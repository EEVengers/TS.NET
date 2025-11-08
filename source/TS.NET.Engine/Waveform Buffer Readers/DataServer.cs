using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TS.NET.Engine;

internal class DataServer : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly IPAddress address;
    private readonly int port;
    private readonly ICaptureBufferReader captureBuffer;

    private CancellationTokenSource? listenerCancelTokenSource;
    private Task? taskListener;
    private Socket? socketListener;

    private CancellationTokenSource? sessionCancelTokenSource;
    private Task? taskSession;
    private Socket? socketSession;

    public DataServer(
        ILogger logger,
        ThunderscopeSettings settings,
        IPAddress address,
        int port,
        ICaptureBufferReader captureBuffer)
    {
        this.logger = logger;
        this.settings = settings;
        this.address = address;
        this.port = port;
        this.captureBuffer = captureBuffer;
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        listenerCancelTokenSource = new CancellationTokenSource();
        taskListener = Task.Factory.StartNew(() => LoopListener(logger, listenerCancelTokenSource.Token), TaskCreationOptions.LongRunning);
        startSemaphore.Release();
    }

    public void Stop()
    {
        listenerCancelTokenSource?.Cancel();
        sessionCancelTokenSource?.Cancel();
        socketListener?.Close();
        socketSession?.Close();
        taskListener?.Wait();
        taskSession?.Wait();
    }

    private void LoopListener(ILogger logger, CancellationToken cancelToken)
    {
        socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socketListener.Bind(new IPEndPoint(address, port));
        socketListener.Listen(backlog: 1);
        logger.LogInformation($"Socket opened {socketListener.LocalEndPoint}");
        try
        {
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                var session = socketListener!.Accept();
                if (socketSession != null)
                {
                    logger.LogInformation($"Dropping session {socketSession?.RemoteEndPoint} and accepting new session");
                    try { socketSession?.Shutdown(SocketShutdown.Both); } catch { }
                    try { socketSession?.Close(); } catch { }
                    sessionCancelTokenSource?.Cancel();
                }
                logger.LogInformation($"Session accepted {session.RemoteEndPoint}");
                sessionCancelTokenSource = new CancellationTokenSource();
                taskSession = Task.Factory.StartNew(() => LoopSession(logger, session, sessionCancelTokenSource.Token), TaskCreationOptions.LongRunning);
                socketSession = session;
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException ex)
        {
            logger.LogDebug($"SocketException: {ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            logger.LogDebug($"Exception: {ex.Message}");
        }
        finally
        {
            if (!cancelToken.IsCancellationRequested)
                logger.LogCritical($"Socket closed");
            else
                logger.LogDebug($"Socket closed");
            socketListener = null;
        }
    }

    private void LoopSession(ILogger logger, Socket client, CancellationToken cancelToken)
    {
        try
        {
            Span<byte> cmdBuf = stackalloc byte[1];
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                int read = 0;
                read = client.Receive(cmdBuf);
                if (read == 0)
                    break;

                byte cmd = cmdBuf[0];
                switch (cmd)
                {
                    case (byte)'K':
                        SendScopehalOld(client, cancelToken);
                        break;
                    case (byte)'S':
                        SendScopehal(client, cancelToken);
                        break;
                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException se)
        {
            logger.LogDebug($"SocketException {se.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            logger.LogDebug($"Exception: {ex.Message}");
        }
        finally
        {
            logger.LogInformation($"Session dropped");
            socketSession = null;
        }
    }

    private uint sequenceNumber = 0;
    private void SendScopehalOld(Socket socket, CancellationToken cancelToken)
    {
        while (true)
        {
            cancelToken.ThrowIfCancellationRequested();
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
                        hwWaveformsPerSec = 0
                    };
                    ChannelHeaderOld chHeader = new()
                    {
                        depth = (ulong)captureMetadata.ProcessingConfig.ChannelDataLength,
                        clipping = 0
                    };
                    if (captureMetadata.Triggered && captureMetadata.ProcessingConfig.TriggerInterpolation)
                    {
                        ReadOnlySpan<sbyte> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<sbyte>(captureMetadata.TriggerChannelCaptureIndex);
                        int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                        if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                        {
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureMetadata.TriggerChannelCaptureIndex);
                            ThunderscopeChannelFrontend triggerChannelFrontend = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            var channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 256.0);
                            var channelOffset = (float)triggerChannelFrontend.ActualVoltOffset;
                            float fa = channelScale * triggerChannelBuffer[triggerIndex - 1] - channelOffset;
                            float fb = channelScale * triggerChannelBuffer[triggerIndex] - channelOffset;
                            float triggerLevel = captureMetadata.ProcessingConfig.EdgeTriggerParameters.LevelV + channelOffset;
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
                        socket.Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeaderOld)));
                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);
                            ThunderscopeChannelFrontend thunderscopeChannel = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            chHeader.channelIndex = (byte)channelIndex;
                            chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 256.0);
                            chHeader.offset = (float)thunderscopeChannel.ActualVoltOffset;
                            socket.Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeaderOld)));
                            var channelBuffer = MemoryMarshal.Cast<sbyte, byte>(captureBuffer.GetChannelReadBuffer<sbyte>(captureBufferIndex));
                            socket.Send(channelBuffer);
                        }
                    }
                    sequenceNumber++;
                    captureBuffer.FinishRead();
                    break;
                }
                else
                    noCapturesAvailable = true;
            }
            if (noCapturesAvailable)
                Thread.Sleep(10);
        }
    }

    private void SendScopehal(Socket socket, CancellationToken cancelToken)
    {
        bool noCapturesAvailable = false;
        while (true)
        {
            cancelToken.ThrowIfCancellationRequested();
            lock (captureBuffer.ReadLock)
            {
                if (captureBuffer.TryStartRead(out var captureMetadata))
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
                        depth = (ulong)captureMetadata.ProcessingConfig.ChannelDataLength,
                        dataType = (byte)captureMetadata.ProcessingConfig.ChannelDataType,
                        trigphase = 0
                    };

                    // If this is a triggered acquisition run trigger interpolation and set trigphase value to be the same for all channels
                    if (captureMetadata.Triggered && captureMetadata.ProcessingConfig.TriggerInterpolation)
                    {

                        switch (captureMetadata.ProcessingConfig.ChannelDataType)
                        {
                            case ThunderscopeDataType.I8:
                                {
                                    ReadOnlySpan<sbyte> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<sbyte>(captureMetadata.TriggerChannelCaptureIndex);
                                    int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                                    if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                                    {
                                        chHeader.trigphase = CalculateTriggerInterpolation(triggerChannelBuffer[triggerIndex - 1], triggerChannelBuffer[triggerIndex]);
                                    }
                                    else
                                    {
                                        chHeader.trigphase = 0;
                                    }
                                }
                                break;
                            case ThunderscopeDataType.I16:
                                {
                                    ReadOnlySpan<short> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<short>(captureMetadata.TriggerChannelCaptureIndex);
                                    int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                                    if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                                    {
                                        chHeader.trigphase = CalculateTriggerInterpolation(triggerChannelBuffer[triggerIndex - 1], triggerChannelBuffer[triggerIndex]);
                                    }
                                    else
                                    {
                                        chHeader.trigphase = 0;
                                    }
                                }
                                break;
                        }
                    }

                    unsafe
                    {
                        socket.Send(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));
                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);
                            ThunderscopeChannelFrontend thunderscopeChannel = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            chHeader.channelIndex = (byte)channelIndex;
                            switch (captureMetadata.ProcessingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 256.0);
                                    break;
                                case ThunderscopeDataType.I16:
                                    chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 65536.0);
                                    break;
                            }
                            chHeader.offset = (float)thunderscopeChannel.ActualVoltOffset;
                            socket.Send(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
                            ReadOnlySpan<byte> channelBuffer = [];
                            switch (captureMetadata.ProcessingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    var channelDataI8 = captureBuffer.GetChannelReadBuffer<sbyte>(captureBufferIndex);
                                    channelBuffer = MemoryMarshal.Cast<sbyte, byte>(channelDataI8);
                                    break;
                                case ThunderscopeDataType.I16:
                                    var channelDataI16 = captureBuffer.GetChannelReadBuffer<short>(captureBufferIndex);
                                    channelBuffer = MemoryMarshal.Cast<short, byte>(channelDataI16);
                                    break;
                            }
                            socket.Send(channelBuffer);
                        }
                    }
                    sequenceNumber++;
                    captureBuffer.FinishRead();
                    break;

                    float CalculateTriggerInterpolation(int pointA, int pointB)
                    {
                        int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                        int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureMetadata.TriggerChannelCaptureIndex);
                        ThunderscopeChannelFrontend triggerChannelFrontend = captureMetadata.HardwareConfig.Frontend[channelIndex];
                        var channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 256.0);     // 8-bit
                        if (captureMetadata.ProcessingConfig.AdcResolution == AdcResolution.TwelveBit)
                            channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 65536.0);   // 16-bit
                        var channelOffset = (float)triggerChannelFrontend.ActualVoltOffset;

                        float fa = channelScale * pointA - channelOffset;
                        float fb = channelScale * pointB - channelOffset;
                        float triggerLevel = captureMetadata.ProcessingConfig.EdgeTriggerParameters.LevelV + channelOffset;
                        float slope = fb - fa;
                        float delta = triggerLevel - fa;
                        float trigphase = delta / slope;
                        if (trigphase > 1.0f || trigphase < -1.0f)
                            trigphase = 0.0f;
                        var result = femtosecondsPerSample * (1 - trigphase);
                        if (!double.IsFinite(result))
                            result = 0;
                        var delay = captureMetadata.ProcessingConfig.TriggerDelayFs - (ulong)triggerIndex * femtosecondsPerSample;
                        result += delay;
                        return result;
                    }
                }
                else
                    noCapturesAvailable = true;
            }
            if (noCapturesAvailable)
                Thread.Sleep(10);
        }
    }
}
