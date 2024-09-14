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
        private readonly ThunderscopeDataBridgeReader bridge;
        private readonly CancellationToken cancellationToken;
        uint sequenceNumber = 0;

        public WaveformSession(
            TcpServer server,
            ILogger logger,
            ThunderscopeDataBridgeReader bridge,
            CancellationToken cancellationToken) : base(server)
        {
            this.logger = logger;
            this.bridge = bridge;
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

                if (bridge.RequestAndWaitForData(500))
                {
                    //logger.LogDebug("Sending waveform...");
                    var dataHeader = bridge.AcquiredDataRegionHeader;
                    var configuration = dataHeader.Hardware;
                    var processing = dataHeader.Processing;
                    var data = bridge.AcquiredDataRegionU8;

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
                        seqnum = sequenceNumber,
                        numChannels = processing.CurrentChannelCount,
                        fsPerSample = femtosecondsPerSample,
                        triggerFs = (long)processing.TriggerDelayFs,
                        hwWaveformsPerSec = bridge.Monitoring.Processing.BridgeWritesPerSec
                    };

                    ChannelHeader chHeader = new()
                    {
                        chNum = 0,
                        depth = processing.CurrentChannelDataLength,
                        scale = 1,
                        offset = 0,
                        trigphase = 0,
                        clipping = 0
                    };

                    ulong bytesSent = 0;

                    // If this is a triggered acquisition run trigger interpolation and set trigphase value to be the same for all channels
                    if (dataHeader.Triggered)
                    {
                        var signedData = bridge.AcquiredDataRegionI8;
                        // To fix - trigger interpolation only works on 4 channel mode
                        var channelData = bridge.Processing.TriggerChannel switch
                        {
                            TriggerChannel.Channel1 => signedData.Slice(0 * (int)processing.CurrentChannelDataLength, (int)processing.CurrentChannelDataLength),
                            TriggerChannel.Channel2 => signedData.Slice(1 * (int)processing.CurrentChannelDataLength, (int)processing.CurrentChannelDataLength),
                            TriggerChannel.Channel3 => signedData.Slice(2 * (int)processing.CurrentChannelDataLength, (int)processing.CurrentChannelDataLength),
                            TriggerChannel.Channel4 => signedData.Slice(3 * (int)processing.CurrentChannelDataLength, (int)processing.CurrentChannelDataLength),
                            _ => throw new NotImplementedException()
                        };
                        // Get the trigger index. If it's greater than 0, then do trigger interpolation.
                        int triggerIndex = (int)(bridge.Processing.TriggerDelayFs / femtosecondsPerSample);
                        if (triggerIndex > 0 && triggerIndex < channelData.Length)
                        {
                            float fa = (chHeader.scale * channelData[triggerIndex - 1]) - chHeader.offset;
                            float fb = (chHeader.scale * channelData[triggerIndex]) - chHeader.offset;
                            float triggerLevel = (chHeader.scale * bridge.Processing.TriggerLevel) + chHeader.offset;
                            float slope = fb - fa;
                            float delta = triggerLevel - fa;
                            float trigphase = delta / slope;
                            chHeader.trigphase = femtosecondsPerSample * (1 - trigphase);
                            //logger.LogTrace("Trigger phase: {0:F6}, first {1}, second {2}", chHeader.trigphase, fa, fb);
                        }
                    }
                    unsafe
                    {
                        Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));
                        bytesSent += (ulong)sizeof(WaveformHeader);
                        //logger.LogDebug("WaveformHeader: " + header.ToString());

                        for (byte channelIndex = 0; channelIndex < processing.CurrentChannelCount; channelIndex++)
                        {
                            ThunderscopeChannelFrontend thunderscopeChannel = configuration.Frontend[channelIndex];
                            chHeader.chNum = channelIndex;
                            chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 255.0);
                            chHeader.offset = (float)thunderscopeChannel.VoltOffset;
                            
                            Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                            bytesSent += (ulong)sizeof(ChannelHeader);
                            //logger.LogDebug("ChannelHeader: " + chHeader.ToString());
                            Send(data.Slice(channelIndex * (int)processing.CurrentChannelDataLength, (int)processing.CurrentChannelDataLength));
                            bytesSent += processing.CurrentChannelDataLength;
                        }
                        //logger.LogDebug($"Sent waveform ({bytesSent} bytes)");
                    }
                    sequenceNumber++;
                    break;
                }
            }
        }

        protected override void OnError(SocketError error)
        {
            logger.LogDebug($"Chat TCP session caught an error with code {error}");
        }
    }

    class DataServer : TcpServer
    {
        private readonly ILogger logger;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ThunderscopeDataBridgeReader bridge;

        public DataServer(ILoggerFactory loggerFactory, ThunderscopeSettings settings, IPAddress address, int port, string bridgeNamespace) : base(address, port)
        {
            logger = loggerFactory.CreateLogger(nameof(DataServer));
            cancellationTokenSource = new();
            bridge = new(bridgeNamespace);
            logger.LogDebug("Started");
        }

        protected override TcpSession CreateSession()
        {
            // ThunderscopeBridgeReader isn't thread safe so here be dragons if multiple clients request a waveform concurrently.
            return new WaveformSession(this, logger, bridge, cancellationTokenSource.Token);
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
