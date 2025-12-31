using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;

namespace TS.NET.Engine;

internal class DataServer : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly IPAddress address;
    private readonly int port;
    private readonly ICaptureBufferReader captureBuffer;
    private readonly ScpiServer scpi;

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
        ICaptureBufferReader captureBuffer,
        ScpiServer scpi)
    {
        this.logger = logger;
        this.settings = settings;
        this.address = address;
        this.port = port;
        this.captureBuffer = captureBuffer;
        this.scpi = scpi;
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
                    if (socketSession != null)
                    {
                        logger.LogInformation($"Dropping data session {socketSession?.RemoteEndPoint} and accepting new session");
                        try { socketSession?.Shutdown(SocketShutdown.Both); } catch { }
                        try { socketSession?.Close(); } catch { }
                        sessionCancelTokenSource?.Cancel();
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

    private void CheckForAcks(ILogger logger, Socket socket)
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
            else
                logger.LogWarning("TODO handle partial read");
        }
    }

    private uint nack = 0;
    private uint inflight = 0;
    private void LoopSession(ILogger logger, Socket socket, CancellationToken cancelToken)
    {
        string sessionID = socket.RemoteEndPoint?.ToString() ?? "Unknown";

        try
        {
            Span<byte> cmdBuf = stackalloc byte[1];
            bool creditMode = false;
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                //New credit-based flow control path
                if (creditMode)
                {
                    CheckForAcks(logger, socket);
                    inflight = sequenceNumber - nack;

                    //Figure out how many un-acked waveforms we have, block if >5 in flight
                    //TODO: tune this for latency/throughput tradeoff?
                    if (inflight >= 5)
                    {
                        cancelToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1));
                        continue;
                    }

                    //Send data
                    else
                    {
                        SendScopehal(socket, cancelToken);
                        //logger.LogInformation($"Sending waveform (seq={sequenceNumber})");
                        scpi.OnUpdateSequence(sequenceNumber);
                    }
                }

                //Legacy path (plus entry to credit mode)
                else
                {
                    int read = 0;
                    read = socket.Receive(cmdBuf);
                    if (read == 0)
                        break;

                    byte cmd = cmdBuf[0];
                    switch (cmd)
                    {
                        case (byte)'K':
                            SendScopehalOld(socket, cancelToken);
                            break;
                        case (byte)'S':
                            SendScopehal(socket, cancelToken);
                            break;
                        case (byte)'C':
                            creditMode = true;
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
                    ulong femtosecondsPerSample = 1000000000000000 / captureMetadata.HardwareConfig.Acquisition.SampleRateHz;
                    WaveformHeaderOld header = new()
                    {
                        seqnum = sequenceNumber,
                        numChannels = (ushort)BitOperations.PopCount(captureMetadata.ProcessingConfig.EnabledChannels),
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
                            int channelIndex = captureMetadata.HardwareConfig.Acquisition.GetChannelIndexByCaptureBufferIndex(captureMetadata.TriggerChannelCaptureIndex);
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
                            int channelIndex = captureMetadata.HardwareConfig.Acquisition.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);
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
                    int channelCount = BitOperations.PopCount(captureMetadata.ProcessingConfig.EnabledChannels);
                    int channelSizeBytes = captureMetadata.ProcessingConfig.ChannelDataType.ByteWidth() * captureMetadata.ProcessingConfig.ChannelDataLength;
                    int waveformHeaderSize;
                    int channelHeaderSize;
                    unsafe
                    {
                        waveformHeaderSize = sizeof(WaveformHeader);
                        channelHeaderSize = sizeof(ChannelHeader);
                    }
                    int totalSendLength = waveformHeaderSize + channelCount * (channelHeaderSize + channelSizeBytes);

                    // Socket.Send is synchronous so build up the send data, release the capture buffer ReadLock then Socket.Send.
                    var poolArray = ArrayPool<byte>.Shared.Rent(totalSendLength);
                    Span<byte> poolSpan = poolArray;
                    int poolSpanPointer = 0;

                    noCapturesAvailable = false;
                    ulong femtosecondsPerSample = 1000000000000000 / captureMetadata.HardwareConfig.Acquisition.SampleRateHz;
                    WaveformHeader waveformHeader = new()
                    {
                        version = 1,
                        seqnum = sequenceNumber,
                        numChannels = (ushort)channelCount,
                        fsPerSample = femtosecondsPerSample,
                        triggerFs = (long)captureMetadata.ProcessingConfig.TriggerDelayFs,
                        hwWaveformsPerSec = captureMetadata.CapturesPerSec
                    };
                    ChannelHeader channelHeader = new()
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
                                    ReadOnlySpan<short> triggerChannelBuffer = captureBuffer.GetChannelReadBuffer<short>(captureMetadata.TriggerChannelCaptureIndex);
                                    int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
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
                        var waveformHeaderSpan = new ReadOnlySpan<byte>(&waveformHeader, sizeof(WaveformHeader));
                        waveformHeaderSpan.CopyTo(poolSpan.Slice(poolSpanPointer, sizeof(WaveformHeader)));
                        poolSpanPointer += sizeof(WaveformHeader);

                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            // Build channel header
                            int channelIndex = captureMetadata.HardwareConfig.Acquisition.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);
                            ThunderscopeChannelFrontend thunderscopeChannel = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            channelHeader.channelIndex = (byte)channelIndex;
                            switch (captureMetadata.ProcessingConfig.ChannelDataType)
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
                            var channelHeaderSpan = new ReadOnlySpan<byte>(&channelHeader, sizeof(ChannelHeader));
                            channelHeaderSpan.CopyTo(poolSpan.Slice(poolSpanPointer, sizeof(ChannelHeader)));
                            poolSpanPointer += sizeof(ChannelHeader);

                            // Build channel buffer
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
                            channelBuffer.CopyTo(poolSpan.Slice(poolSpanPointer, channelBuffer.Length));
                            poolSpanPointer += channelBuffer.Length;
                        }
                    }
                    sequenceNumber++;
                    captureBuffer.FinishRead();
                    if (poolSpanPointer != totalSendLength)
                        throw new ThunderscopeException("Bytes written to span don't match expected length");
                    socket.Send(poolSpan.Slice(0, poolSpanPointer));
                    ArrayPool<byte>.Shared.Return(poolArray);
                    break;

                    float CalculateTriggerInterpolation(int pointA, int pointB)
                    {
                        int triggerIndex = (int)(captureMetadata.ProcessingConfig.TriggerDelayFs / femtosecondsPerSample);
                        int channelIndex = captureMetadata.HardwareConfig.Acquisition.GetChannelIndexByCaptureBufferIndex(captureMetadata.TriggerChannelCaptureIndex);
                        ThunderscopeChannelFrontend triggerChannelFrontend = captureMetadata.HardwareConfig.Frontend[channelIndex];
                        var channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 256.0);     // 8-bit
                        if (captureMetadata.ProcessingConfig.AdcResolution == AdcResolution.TwelveBit)
                            channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 65536.0);   // 16-bit
                            //channelScale = (float)(triggerChannelFrontend.ActualVoltFullScale / 4096.0);   // 16-bit
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
            {
                cancelToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(1));
            }
        }
    }
}
