using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TS.NET.Engine
{
    internal class SocketTask
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct WaveformHeader
        {
            internal uint seqnum;
            internal ushort numChannels;
            internal ulong fsPerSample;
            internal ulong triggerFs;
            internal double hwWaveformsPerSec;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct ChannelHeader
        {
            internal byte chNum;
            internal ulong depth;
            internal float scale;
            internal float offset;
            internal float trigphase;
            internal byte clipping;
        }

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;
        private Socket listener;

        public void Start(ILoggerFactory loggerFactory, ThunderscopeSettings settings, BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel)
        {
            var logger = loggerFactory.CreateLogger(nameof(SocketTask));
            cancelTokenSource = new CancellationTokenSource();
            ThunderscopeBridgeReader bridge = new(new ThunderscopeBridgeOptions("ThunderScope.1", 4, settings.MaxChannelBytes));
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 5026);
            listener = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.LingerState = new LingerOption(true, 1);
            listener.Bind(localEndPoint);
            taskLoop = Task.Factory.StartNew(() => Loop(logger, bridge, listener, processingRequestChannel, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            listener.Close();
            taskLoop?.Wait();
        }

        private static void Loop(
            ILogger logger,
            ThunderscopeBridgeReader bridge,
            Socket listener,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = "TS.NET Socket";
            Socket clientSocket = null;

            try
            {
                logger.LogInformation("Starting data plane socket server at :5026");
                listener.Listen(10);
                clientSocket = listener.Accept();
                clientSocket.NoDelay = true;
                logger.LogInformation("Client connected to data plane");

                uint seqnum = 0;
                clientSocket.NoDelay = true;

                while (true)
                {
                    byte[] bytes = new byte[1];

                    // Wait for flow control 'K'
                    while (true)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        if (!clientSocket.Poll(10_000, SelectMode.SelectRead)) continue;
                        int numByte = clientSocket.Receive(bytes);
                        if (numByte != 0) break;
                    }

                    //logger.LogDebug("Got request for waveform...");

                    while (true)
                    {
                        cancelToken.ThrowIfCancellationRequested();

                        if (bridge.RequestAndWaitForData(500))
                        {
                            //logger.LogDebug("Send waveform...");
                            var configuration = bridge.Configuration;
                            var processing = bridge.Processing;
                            var data = bridge.AcquiredRegionAsByte;

                            // Remember AdcChannelMode reflects the hardware reality - user may only have 3 channels enabled but hardware has to capture 4.
                            ulong femtosecondsPerSample = configuration.AdcChannelMode switch
                            {
                                AdcChannelMode.Single => 1000000,         // 1 GSPS
                                AdcChannelMode.Dual => 1000000 * 2,     // 500 MSPS
                                AdcChannelMode.Quad => 1000000 * 4,    // 250 MSPS
                                _ => throw new NotImplementedException(),
                            };

                            WaveformHeader header = new()
                            {
                                seqnum = seqnum,
                                numChannels = processing.CurrentChannelCount,
                                fsPerSample = femtosecondsPerSample,
                                triggerFs = 0,
                                hwWaveformsPerSec = 1
                            };

                            ChannelHeader chHeader = new()
                            {
                                chNum = 0,
                                depth = processing.CurrentChannelBytes,
                                scale = 1,
                                offset = 0,
                                trigphase = 0,
                                clipping = 0
                            };

                            unsafe
                            {
                                clientSocket.Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));

                                for (byte channel = 0; channel < processing.CurrentChannelCount; channel++)
                                {
                                    ThunderscopeChannel thunderscopeChannel = configuration.GetChannel(channel);

                                    float full_scale = ((float)thunderscopeChannel.VoltsDiv / 1000f) * 5f; // 5 instead of 10 for signed

                                    chHeader.chNum = channel;
                                    chHeader.scale = full_scale / 127f; // 127 instead of 255 for signed
                                    chHeader.offset = -((float)thunderscopeChannel.VoltsOffset); // needs chHeader.scale * 0x80 for signed

                                    // if (ch == 0)
                                    //     logger.LogDebug($"ch {ch}: VoltsDiv={tChannel.VoltsDiv} -> .scale={chHeader.scale}, VoltsOffset={tChannel.VoltsOffset} -> .offset = {chHeader.offset}, Coupling={tChannel.Coupling}");
                                    
                                    // Length of this channel as 'depth'
                                    clientSocket.Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                                    clientSocket.Send(data.Slice(channel * (int)processing.CurrentChannelBytes, (int)processing.CurrentChannelBytes));
                                }
                            }

                            seqnum++;

                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Stopping...");
            }
            catch (SocketException ex)
            {
                if (!ex.Message.Contains("WSACancelBlockingCall"))      // On Windows; can use this string to ignore the SocketException thrown when listener.Close() called
                    throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"Error");
                throw;
            }
            finally
            {
                try
                {
                    clientSocket?.Shutdown(SocketShutdown.Both);
                    clientSocket?.Close();
                }
                catch (Exception ex) { }

                logger.LogDebug($"Stopped");
            }
        }
    }
}