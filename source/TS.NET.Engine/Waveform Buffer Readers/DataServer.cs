using Microsoft.Extensions.Logging;
using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TS.NET.Engine;

// Note: ICaptureBufferReader, if used properly, is thread safe however if multiple clients connect then it will be lossy
// (the data sent to a client won't be sent to any others if multiple request data)
internal class DataServer : TcpServer, IThread
{
    private readonly ILogger logger;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly ICaptureBufferReader captureBuffer;

    public DataServer(
        ILogger logger, 
        ThunderscopeSettings settings, 
        IPAddress address, 
        int port, 
        ICaptureBufferReader captureBuffer) : base(address, port)
    {
        this.logger = logger;
        cancellationTokenSource = new();
        this.captureBuffer = captureBuffer;
        logger.LogDebug("Started");
    }

    protected override TcpSession CreateSession()
    {
        return new WaveformSession(this, logger, captureBuffer, cancellationTokenSource.Token);
    }

    protected override void OnError(SocketError error)
    {
        logger.LogDebug($"Waveform server caught an error with code {error}");
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        base.Start();
        startSemaphore.Release();
    }

    public new void Stop()
    {
        base.Stop();
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WaveformHeaderOld
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
internal struct WaveformHeader
{
    internal byte version;      // Starts from 1
    internal uint seqnum;
    internal ushort numChannels;
    internal ulong fsPerSample;
    internal long triggerFs;
    internal double hwWaveformsPerSec;

    public override string ToString()
    {
        return $"seqnum: {seqnum}, numChannels: {numChannels}, fsPerSample: {fsPerSample}, triggerFs: {triggerFs}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ChannelHeaderOld
{
    internal byte channelIndex;
    internal ulong depth;
    internal float scale;
    internal float offset;
    internal float trigphase;
    internal byte clipping;
    public override string ToString()
    {
        return $"chNum: {channelIndex}, depth: {depth}, scale: {scale}, offset: {offset}, trigphase: {trigphase}, clipping: {clipping}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ChannelHeader
{
    internal byte channelIndex;
    internal ulong depth;
    internal float scale;
    internal float offset;
    internal float trigphase;
    internal byte clipping;
    internal byte dataType;             // ThunderscopeDataType, I8 = 2, I16 = 4
    public override string ToString()
    {
        return $"chNum: {channelIndex}, depth: {depth}, scale: {scale}, offset: {offset}, trigphase: {trigphase}";
    }
}

internal class WaveformSession : TcpSession
{
    private readonly ILogger logger;
    private readonly ICaptureBufferReader captureBuffer;
    private readonly CancellationToken cancellationToken;
    private uint sequenceNumber = 0;

    public WaveformSession(TcpServer server, ILogger logger, ICaptureBufferReader captureBuffer, CancellationToken cancellationToken) : base(server)
    {
        this.logger = logger;
        this.captureBuffer = captureBuffer;
        this.cancellationToken = cancellationToken;
    }

    protected override void OnConnected()
    {
        logger.LogDebug($"Waveform session with Id {Id} connected!");
    }

    protected override void OnDisconnected()
    {
        logger.LogDebug($"Waveform session with Id {Id} disconnected!");
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size == 0)
            return;

        switch (buffer[0])
        {
            case (byte)'K':         // Scopehal old format.
                SendScopehalOld();
                break;
            case (byte)'S':         // 'S'copehal new format, with version field for futureproofing.
                SendScopehal();
                break;
                //case (byte)'T':         // 'T'S.NET format, with idempotency by transmitting full instrument & processing configuration with data.
        }

    }

    protected override void OnError(SocketError error)
    {
        logger.LogDebug($"Chat TCP session caught an error with code {error}");
    }

    private void SendScopehalOld()
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool noCapturesAvailable = false;
            lock (captureBuffer.ReadLock)
            {
                if (captureBuffer.TryStartRead(out var captureMetadata))
                {
                    ulong femtosecondsPerSample = 1000000000000000 / captureMetadata.HardwareConfig.SampleRateHz;

                    WaveformHeaderOld header = new()
                    {
                        seqnum = sequenceNumber,
                        numChannels = captureMetadata.ProcessingConfig.ChannelCount,
                        fsPerSample = femtosecondsPerSample,
                        triggerFs = (long)captureMetadata.ProcessingConfig.TriggerDelayFs,
                        hwWaveformsPerSec = 0// bridge.Monitoring.Processing.BridgeWritesPerSec
                    };

                    ChannelHeaderOld chHeader = new()
                    {
                        // All other values set later
                        depth = (ulong)captureMetadata.ProcessingConfig.ChannelDataLength,
                        clipping = 0
                    };

                    ulong bytesSent = 0;

                    // If this is a triggered acquisition run trigger interpolation and set trigphase value to be the same for all channels
                    if (captureMetadata.Triggered && captureMetadata.ProcessingConfig.TriggerInterpolation)
                    {
                        ReadOnlySpan<sbyte> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<sbyte>(captureMetadata.TriggerChannelCaptureIndex);
                        // Get the trigger index. If it's greater than 0, then do trigger interpolation.
                        int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                        if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                        {
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureMetadata.TriggerChannelCaptureIndex);
                            ThunderscopeChannelFrontend triggerChannelFrontend = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            var channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 255.0);
                            var channelOffset = (float)triggerChannelFrontend.ActualVoltOffset;

                            float fa = channelScale * triggerChannelBuffer[triggerIndex - 1] - channelOffset;
                            float fb = channelScale * triggerChannelBuffer[triggerIndex] - channelOffset;
                            float triggerLevel = channelScale * captureMetadata.ProcessingConfig.EdgeTriggerParameters.Level + channelOffset;
                            float slope = fb - fa;
                            float delta = triggerLevel - fa;
                            float trigphase = delta / slope;
                            chHeader.trigphase = femtosecondsPerSample * (1 - trigphase);
                            if (!double.IsFinite(chHeader.trigphase))
                                chHeader.trigphase = 0;
                            var delay = captureMetadata.ProcessingConfig.TriggerDelayFs - (ulong)triggerIndex * femtosecondsPerSample;
                            chHeader.trigphase += delay;
                        }
                    }
                    unsafe
                    {
                        Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeaderOld)));
                        bytesSent += (ulong)sizeof(WaveformHeaderOld);

                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            // Map captureBufferIndex to channelIndex
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);

                            ThunderscopeChannelFrontend thunderscopeChannel = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            chHeader.channelIndex = (byte)channelIndex;
                            chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 255.0);
                            chHeader.offset = (float)thunderscopeChannel.ActualVoltOffset;

                            Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeaderOld)));
                            bytesSent += (ulong)sizeof(ChannelHeaderOld);
                            var channelBuffer = MemoryMarshal.Cast<sbyte, byte>(captureBuffer.GetChannelReadBuffer<sbyte>(captureBufferIndex));
                            Send(channelBuffer);
                            bytesSent += (ulong)captureMetadata.ProcessingConfig.ChannelDataLength;
                        }
                    }
                    sequenceNumber++;
                    captureBuffer.FinishRead();
                    break;
                }
                else
                {
                    noCapturesAvailable = true;
                }
            }
            if (noCapturesAvailable)
            {
                Thread.Sleep(10);
            }
        }
    }

    private void SendScopehal()
    {
        bool noCapturesAvailable = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (captureBuffer.ReadLock)
            {
                if (captureBuffer.TryStartRead(out var captureMetadata))      // Add timeout parameter and eliminate Thread.Sleep
                {
                    noCapturesAvailable = false;
                    ulong femtosecondsPerSample = 1000000000000000 / captureMetadata.HardwareConfig.SampleRateHz;

                    WaveformHeader header = new()
                    {
                        version = 1,
                        seqnum = sequenceNumber,
                        numChannels = captureMetadata.ProcessingConfig.ChannelCount,
                        fsPerSample = femtosecondsPerSample,
                        triggerFs = (long)captureMetadata.ProcessingConfig.TriggerDelayFs,
                        hwWaveformsPerSec = captureMetadata.CapturesPerSec
                    };

                    ChannelHeader chHeader = new()
                    {
                        // All other values set later
                        depth = (ulong)captureMetadata.ProcessingConfig.ChannelDataLength,
                        dataType = (byte)captureMetadata.ProcessingConfig.ChannelDataType
                    };

                    ulong bytesSent = 0;

                    // If this is a triggered acquisition run trigger interpolation and set trigphase value to be the same for all channels
                    if (captureMetadata.Triggered && captureMetadata.ProcessingConfig.TriggerInterpolation)
                    {
                        if (captureMetadata.ProcessingConfig.ChannelDataType == ThunderscopeDataType.I8)
                        {
                            ReadOnlySpan<sbyte> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<sbyte>(captureMetadata.TriggerChannelCaptureIndex);
                            // Get the trigger index. If it's greater than 0, then do trigger interpolation.
                            int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                            if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                            {
                                int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureMetadata.TriggerChannelCaptureIndex);
                                ThunderscopeChannelFrontend triggerChannelFrontend = captureMetadata.HardwareConfig.Frontend[channelIndex];
                                var channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 255.0);
                                var channelOffset = (float)triggerChannelFrontend.ActualVoltOffset;

                                float fa = channelScale * triggerChannelBuffer[triggerIndex - 1] - channelOffset;
                                float fb = channelScale * triggerChannelBuffer[triggerIndex] - channelOffset;
                                float triggerLevel = channelScale * captureMetadata.ProcessingConfig.EdgeTriggerParameters.Level + channelOffset;
                                float slope = fb - fa;
                                float delta = triggerLevel - fa;
                                float trigphase = delta / slope;
                                chHeader.trigphase = femtosecondsPerSample * (1 - trigphase);
                                if (!double.IsFinite(chHeader.trigphase))
                                    chHeader.trigphase = 0;
                                var delay = captureMetadata.ProcessingConfig.TriggerDelayFs - (ulong)triggerIndex * femtosecondsPerSample;
                                chHeader.trigphase += delay;
                            }
                        }
                        else
                            chHeader.trigphase = 0;
                    }
                    unsafe
                    {
                        Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));
                        bytesSent += (ulong)sizeof(WaveformHeader);

                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            // Map captureBufferIndex to channelIndex
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);

                            ThunderscopeChannelFrontend thunderscopeChannel = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            chHeader.channelIndex = (byte)channelIndex;
                            chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 255.0);
                            chHeader.offset = (float)thunderscopeChannel.ActualVoltOffset;

                            Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                            bytesSent += (ulong)sizeof(ChannelHeader);
                            ReadOnlySpan<byte> channelBuffer = [];
                            switch(captureMetadata.ProcessingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    channelBuffer = MemoryMarshal.Cast<sbyte, byte>(captureBuffer.GetChannelReadBuffer<sbyte>(captureBufferIndex));
                                    break;
                                case ThunderscopeDataType.I16:
                                    channelBuffer = MemoryMarshal.Cast<short, byte>(captureBuffer.GetChannelReadBuffer<short>(captureBufferIndex));
                                    break;
                            }
                            Send(channelBuffer);
                            bytesSent += (ulong)captureMetadata.ProcessingConfig.ChannelDataLength;
                        }
                    }
                    sequenceNumber++;
                    captureBuffer.FinishRead();
                    break;
                }
                else
                {
                    noCapturesAvailable = true;
                }
            }
            if (noCapturesAvailable)
            {
                Thread.Sleep(10);
            }
        }
    }
}
