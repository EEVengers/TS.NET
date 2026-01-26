using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace TS.NET.Engine;

internal class DataServer : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly IPAddress address;
    private readonly int port;
    private readonly ICaptureBufferManagerReader captureBufferManager;
    private readonly Action<uint> onSequenceUpdate;

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
        ICaptureBufferManagerReader captureBuffer,
        Action<uint> onSequenceUpdate)
    {
        this.logger = logger;
        this.settings = settings;
        this.address = address;
        this.port = port;
        this.captureBufferManager = captureBuffer;
        this.onSequenceUpdate = onSequenceUpdate;
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
        while (!cancelToken.IsCancellationRequested)
        {
            socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketListener.Bind(new IPEndPoint(address, port));
            socketListener.Listen(backlog: 1);
            logger.LogInformation($"Data socket listening {socketListener.LocalEndPoint}");
            try
            {
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    var session = socketListener!.Accept();
                    session.NoDelay = true;
                    if (taskSession != null)
                    {
                        logger.LogInformation($"Dropping data session ({socketSession?.RemoteEndPoint?.ToString() ?? "Unknown"}) and accepting new session");
                        try { socketSession?.Shutdown(SocketShutdown.Both); } catch { }
                        try { socketSession?.Close(); } catch { }
                        sessionCancelTokenSource?.Cancel();
                        taskSession?.Wait();
                    }
                    logger.LogInformation($"Data session accepted ({session.RemoteEndPoint})");
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
                    logger.LogCritical($"Data socket closed");
                else
                    logger.LogDebug($"Data socket closed");
                socketListener = null;
            }
        }
    }

    private void LoopSession(ILogger logger, Socket socket, CancellationToken cancelToken)
    {
        string sessionID = socket.RemoteEndPoint?.ToString() ?? "Unknown";
        uint sequenceNumber = 0;
        uint nack = 0;
        uint inflight = 0;

        try
        {
            Span<byte> cmdBuf = stackalloc byte[1];
            bool creditMode = false;
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                if (creditMode)
                {
                    if (!CheckForAcks(logger, socket))
                        break;
                    inflight = sequenceNumber - nack;

                    // Figure out how many un-acked waveforms we have, block if >5 in flight
                    // TODO: tune this for latency/throughput tradeoff?
                    if (inflight >= 5)
                    {
                        cancelToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1));
                        continue;
                    }
                    else
                    {
                        //logger.LogInformation($"Sending waveform (seq={sequenceNumber})");
                        SendScopehal(socket, sequenceNumber, cancelToken);
                        onSequenceUpdate(sequenceNumber);
                        sequenceNumber++;
                    }
                }
                else
                {
                    int read = 0;
                    read = socket.Receive(cmdBuf);
                    if (read == 0)
                        break;

                    byte cmd = cmdBuf[0];
                    switch (cmd)
                    {
                        case (byte)'S':
                            SendScopehal(socket, sequenceNumber, cancelToken);
                            sequenceNumber++;
                            break;
                        case (byte)'C':
                            creditMode = true;
                            logger.LogInformation("Switching to credit mode");
                            break;
                        default:
                            break;
                    }
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
            logger.LogInformation($"Session dropped ({sessionID})");
            socketSession = null;
        }

        bool CheckForAcks(ILogger logger, Socket socket)
        {
            //See if we have data ready to read. Grab the ACKs if so (may be >1 queued)
            Span<byte> ack = stackalloc byte[4];
            while (socket.Poll(1000, SelectMode.SelectRead))
            {
                //Get the ACK number (if we see one).
                //TODO: this assumes all 4 bytes are always in the same TCP segment. Probably reasonable
                //but for max robustness we'd want to handle partial acks somehow
                int read = 0;
                read = socket.Receive(ack);
                if (read == 4)
                {
                    nack = BitConverter.ToUInt32(ack);
                    inflight = sequenceNumber - nack;
                    //logger.LogInformation($"Got ACK: {nack}, last sequenceNumber={sequenceNumber}, {inflight} in flight");
                }

                //If socket is closed or we have a read error, bail out
                else if (read <= 0)
                {
                    //logger.LogWarning($"Read returned {read}");
                    return false;
                }

                else
                    logger.LogWarning("TODO handle partial read");
            }

            return true;
        }
    }

    private void SendScopehal(Socket socket, uint sequenceNumber, CancellationToken cancelToken)
    {
        bool loop = true;
        while (loop)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (captureBufferManager.TryStartRead(out var captureBuffer))
            {
                try
                {
                    int channelCount = BitOperations.PopCount(captureBuffer!.Metadata.HardwareConfig.Acquisition.EnabledChannels);
                    ulong femtosecondsPerSample = 1000000000000000 / captureBuffer.Metadata.HardwareConfig.Acquisition.SampleRateHz;
                    WaveformHeader waveformHeader = new()
                    {
                        version = 1,
                        seqnum = sequenceNumber,
                        numChannels = (ushort)channelCount,
                        fsPerSample = femtosecondsPerSample,
                        triggerFs = (long)captureBuffer.Metadata.ProcessingConfig.TriggerDelayFs,
                        hwWaveformsPerSec = captureBuffer.Metadata.CapturesPerSec
                    };
                    ChannelHeader channelHeader = new()
                    {
                        depth = (ulong)captureBuffer.Metadata.ProcessingConfig.ChannelDataLength,
                        dataType = (byte)captureBuffer.Metadata.ProcessingConfig.ChannelDataType,
                        trigphase = 0
                    };

                    // If this is a triggered acquisition run trigger interpolation and set trigphase value to be the same for all channels
                    if (captureBuffer.Metadata.Triggered && captureBuffer.Metadata.ProcessingConfig.TriggerInterpolation)
                    {
                        switch (captureBuffer.Metadata.ProcessingConfig.ChannelDataType)
                        {
                            case ThunderscopeDataType.I8:
                                {
                                    ReadOnlySpan<sbyte> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<sbyte>(captureBuffer.Metadata.TriggerChannelCaptureIndex);
                                    int triggerIndex = (int)(captureBuffer.Metadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                                    if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                                    {
                                        channelHeader.trigphase = CalculateTriggerInterpolation(triggerChannelBuffer[triggerIndex - 1], triggerChannelBuffer[triggerIndex]);
                                    }
                                    else
                                    {
                                        channelHeader.trigphase = 0;
                                    }
                                }
                                break;
                            case ThunderscopeDataType.I16:
                                {
                                    ReadOnlySpan<short> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<short>(captureBuffer.Metadata.TriggerChannelCaptureIndex);
                                    int triggerIndex = (int)(captureBuffer.Metadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                                    if (triggerIndex > 0 && triggerIndex < triggerChannelBuffer.Length)
                                    {
                                        channelHeader.trigphase = CalculateTriggerInterpolation(triggerChannelBuffer[triggerIndex - 1], triggerChannelBuffer[triggerIndex]);
                                    }
                                    else
                                    {
                                        channelHeader.trigphase = 0;
                                    }
                                }
                                break;
                        }
                    }

                    unsafe
                    {
                        socket.Send(new ReadOnlySpan<byte>(&waveformHeader, sizeof(WaveformHeader)));
                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            // Build channel header
                            int channelIndex = captureBuffer.Metadata.HardwareConfig.Acquisition.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);
                            ThunderscopeChannelFrontend thunderscopeChannel = captureBuffer.Metadata.HardwareConfig.Frontend[channelIndex];
                            channelHeader.channelIndex = (byte)channelIndex;
                            switch (captureBuffer.Metadata.ProcessingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    channelHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 256.0);
                                    break;
                                case ThunderscopeDataType.I16:
                                    channelHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 65536.0);
                                    //chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 4096.0);
                                    break;
                            }
                            channelHeader.offset = (float)thunderscopeChannel.ActualVoltOffset;
                            socket.Send(new ReadOnlySpan<byte>(&channelHeader, sizeof(ChannelHeader)));
                            ReadOnlySpan<byte> channelBuffer = captureBuffer.GetChannelReadByteBuffer(captureBufferIndex);
                            socket.Send(channelBuffer);
                        }
                    }

                    float CalculateTriggerInterpolation(int pointA, int pointB)
                    {
                        int triggerIndex = (int)(captureBuffer.Metadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                        int channelIndex = captureBuffer.Metadata.HardwareConfig.Acquisition.GetChannelIndexByCaptureBufferIndex(captureBuffer.Metadata.TriggerChannelCaptureIndex);
                        ThunderscopeChannelFrontend triggerChannelFrontend = captureBuffer.Metadata.HardwareConfig.Frontend[channelIndex];
                        var channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 256.0);     // 8-bit
                        if (captureBuffer.Metadata.HardwareConfig.Acquisition.Resolution == AdcResolution.TwelveBit)
                            channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 65536.0);   // 16-bit
                                                                                                            //channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 4096.0);   // 16-bit
                        var channelOffset = (float)triggerChannelFrontend.ActualVoltOffset;

                        float fa = channelScale * pointA - channelOffset;
                        float fb = channelScale * pointB - channelOffset;
                        float triggerLevel = captureBuffer.Metadata.ProcessingConfig.EdgeTriggerParameters.LevelV + channelOffset;
                        float slope = fb - fa;
                        float delta = triggerLevel - fa;
                        float trigphase = delta / slope;
                        if (trigphase > 1.0f || trigphase < -1.0f)
                            trigphase = 0.0f;
                        var result = femtosecondsPerSample * (1 - trigphase);
                        if (!double.IsFinite(result))
                            result = 0;
                        var delay = captureBuffer.Metadata.ProcessingConfig.TriggerDelayFs - (ulong)triggerIndex * femtosecondsPerSample;
                        result += delay;
                        return result;
                    }
                }
                finally
                {
                    captureBufferManager.FinishRead();
                    loop = false;
                }
            }
            else
            {
                cancelToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1));
            }
        }
    }
}
