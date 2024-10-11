using Microsoft.Extensions.Logging;
using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TS.NET.Engine
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct WaveformHeader
    {
        internal uint seqnum;
        internal ushort numChannels;
        internal ulong fsPerSample;
        internal long triggerFs;
        internal double hwWaveformsPerSec;

        public override string ToString()
        {
            return $"seqnum: {seqnum}, numChannels: {numChannels}, fsPerSample: {fsPerSample}, triggerFs: {triggerFs}, hwWaveformsPerSec: {hwWaveformsPerSec}";
        }
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
        public override string ToString()
        {
            return $"chNum: {chNum}, depth: {depth}, scale: {scale}, offset: {offset}, trigphase: {trigphase}, clipping: {clipping}";
        }
    }

    internal class WaveformSession : TcpSession
    {
        private readonly ILogger logger;
        private readonly ChannelCaptureCircularBufferI8 captureBuffer;
        private readonly CancellationToken cancellationToken;
        private uint sequenceNumber = 0;

        public WaveformSession(TcpServer server, ILogger logger, ChannelCaptureCircularBufferI8 captureBuffer, CancellationToken cancellationToken) : base(server)
        {
            this.logger = logger;
            this.captureBuffer = captureBuffer;
            this.cancellationToken = cancellationToken;
        }

        protected override void OnConnected()
        {
            logger.LogDebug($"Waveform session with Id {Id} connected!");
            //string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
            //SendAsync(message);
        }

        protected override void OnDisconnected()
        {
            logger.LogDebug($"Waveform session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            if (size == 0)
                return;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (captureBuffer.CurrentCaptureCount > 0)
                {
                    lock (captureBuffer)
                    {
                        if (captureBuffer.TryStartRead(out var triggered))      // Add timeout parameter
                        {
                            //logger.LogDebug("Sending waveform...");
                            //var dataHeader = bridge.AcquiredDataRegionHeader;
                            var hardware = captureBuffer.Hardware;
                            var processing = captureBuffer.Processing;
                            //var data = bridge.AcquiredDataRegionU8;

                            // Remember AdcChannelMode reflects the hardware reality - user may only have 3 channels enabled but hardware has to capture 4.
                            ulong femtosecondsPerSample = 1000000000000000 / hardware.SampleRateHz;

                            WaveformHeader header = new()
                            {
                                seqnum = sequenceNumber,
                                numChannels = processing.ChannelCount,
                                fsPerSample = femtosecondsPerSample,
                                triggerFs = (long)processing.TriggerDelayFs,
                                hwWaveformsPerSec = 0// bridge.Monitoring.Processing.BridgeWritesPerSec
                            };

                            ChannelHeader chHeader = new()
                            {
                                chNum = 0,
                                depth = (ulong)processing.ChannelDataLength,
                                scale = 1,
                                offset = 0,
                                trigphase = 0,
                                clipping = 0
                            };

                            ulong bytesSent = 0;

                            // If this is a triggered acquisition run trigger interpolation and set trigphase value to be the same for all channels
                            if (triggered)
                            {
                                //var signedData = bridge.AcquiredDataRegionI8;
                                // To fix - trigger interpolation only works on 4 channel mode
                                var channelData = processing.TriggerChannel switch
                                {
                                    TriggerChannel.Channel1 => captureBuffer.GetReadBuffer(0),
                                    TriggerChannel.Channel2 => captureBuffer.GetReadBuffer(1),
                                    TriggerChannel.Channel3 => captureBuffer.GetReadBuffer(2),
                                    TriggerChannel.Channel4 => captureBuffer.GetReadBuffer(3),
                                    _ => throw new NotImplementedException()
                                };
                                // Get the trigger index. If it's greater than 0, then do trigger interpolation.
                                int triggerIndex = (int)(processing.TriggerDelayFs / femtosecondsPerSample);
                                if (triggerIndex > 0 && triggerIndex < channelData.Length)
                                {
                                    float fa = (chHeader.scale * channelData[triggerIndex - 1]) - chHeader.offset;
                                    float fb = (chHeader.scale * channelData[triggerIndex]) - chHeader.offset;
                                    float triggerLevel = (chHeader.scale * processing.TriggerLevel) + chHeader.offset;
                                    float slope = fb - fa;
                                    float delta = triggerLevel - fa;
                                    float trigphase = delta / slope;
                                    chHeader.trigphase = femtosecondsPerSample * (1 - trigphase);
                                    if (!double.IsFinite(chHeader.trigphase))
                                        chHeader.trigphase = 0;
                                    //logger.LogTrace("Trigger phase: {0:F6}, first {1}, second {2}", chHeader.trigphase, fa, fb);
                                }
                            }
                            unsafe
                            {
                                Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));
                                bytesSent += (ulong)sizeof(WaveformHeader);
                                //logger.LogDebug("WaveformHeader: " + header.ToString());

                                for (byte channelIndex = 0; channelIndex < processing.ChannelCount; channelIndex++)
                                {
                                    ThunderscopeChannelFrontend thunderscopeChannel = hardware.Frontend[channelIndex];
                                    chHeader.chNum = channelIndex;
                                    chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 255.0);
                                    chHeader.offset = (float)thunderscopeChannel.VoltOffset;

                                    Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                                    bytesSent += (ulong)sizeof(ChannelHeader);
                                    //logger.LogDebug("ChannelHeader: " + chHeader.ToString());
                                    var channelBuffer = MemoryMarshal.Cast<sbyte, byte>(captureBuffer.GetReadBuffer(channelIndex));
                                    Send(channelBuffer);
                                    bytesSent += (ulong)processing.ChannelDataLength;
                                }
                                //logger.LogDebug($"Sent waveform ({bytesSent} bytes)");
                            }
                            sequenceNumber++;
                            captureBuffer.FinishRead();
                            break;
                        }
                    }
                }
                
                Thread.Sleep(0);        // Change this later to a timeout mechanism
            }
        }

        protected override void OnError(SocketError error)
        {
            logger.LogDebug($"Chat TCP session caught an error with code {error}");
        }
    }

    internal class DataServer : TcpServer
    {
        private readonly ILogger logger;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ChannelCaptureCircularBufferI8 captureBuffer;

        public DataServer(ILoggerFactory loggerFactory, ThunderscopeSettings settings, IPAddress address, int port, ChannelCaptureCircularBufferI8 captureBuffer) : base(address, port)
        {
            logger = loggerFactory.CreateLogger(nameof(DataServer));
            cancellationTokenSource = new();
            this.captureBuffer = captureBuffer;
            logger.LogDebug("Started");
        }

        protected override TcpSession CreateSession()
        {
            // ThunderscopeBridgeReader isn't thread safe so here be dragons if multiple clients request a waveform concurrently.
            return new WaveformSession(this, logger, captureBuffer, cancellationTokenSource.Token);
        }

        protected override void OnError(SocketError error)
        {
            logger.LogDebug($"Waveform server caught an error with code {error}");
        }

        protected override void OnStopping()
        {
            base.OnStopping();
        }
    }
}
