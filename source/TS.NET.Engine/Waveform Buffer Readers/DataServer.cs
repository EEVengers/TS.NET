using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TS.NET.Engine;

internal class DataServer : IThread
{
    private readonly ILogger logger;
    
    private readonly ICaptureBufferReader captureBuffer;

    private readonly IPAddress address;
    private readonly int port;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskListener;
    private Task? taskClient;

    public DataServer(
        ILogger logger,
        ThunderscopeSettings settings,
        IPAddress address,
        int port,
        ICaptureBufferReader captureBuffer)
    {
        this.logger = logger;
        this.address = address;
        this.port = port;
        this.captureBuffer = captureBuffer;
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        cancelTokenSource = new CancellationTokenSource();
        taskListener = Task.Factory.StartNew(() => ListenerLoop(logger, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        startSemaphore.Release();
    }

    public void Stop()
    {
        cancelTokenSource?.Cancel();
        taskListener?.Wait();
        taskClient?.Wait();
        taskClient = null;
    }

    private void ListenerLoop(ILogger logger, CancellationToken cancelToken)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(address, port));
        listener.Listen(backlog: 1);

        while (!cancelToken.IsCancellationRequested)
        {
            try
            {
                var client = listener!.Accept();
                logger.LogDebug($"Client accepted: {client.RemoteEndPoint}");

                    if (taskClient != null)
                    {
                        logger.LogDebug("A session is already active; rejecting new connection");
                        try { client.Shutdown(SocketShutdown.Both); } catch { }
                        try { client.Close(); } catch { }
                        continue;
                    }

                    taskClient = Task.Factory.StartNew(() => ClientLoop(logger, client, cancelTokenSource.Token), TaskCreationOptions.LongRunning);              
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (cancelToken.IsCancellationRequested) break;
                logger.LogDebug($"Accept error: {ex.SocketErrorCode}");
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                if (cancelToken.IsCancellationRequested) break;
                logger.LogDebug($"Accept exception: {ex.Message}");
                Thread.Sleep(50);
            }
        }

        listener.Close();
    }

    private void ClientLoop(ILogger logger, Socket client, CancellationToken cancelToken)
    {
        Span<byte> cmdBuf = stackalloc byte[1];
        try
        {
            while (!cancelToken.IsCancellationRequested)
            {
                int read = 0;
                try
                {
                    read = client.Receive(cmdBuf);
                }
                catch (SocketException se)
                {
                    logger.LogDebug($"Receive error {se.SocketErrorCode}");
                    break;
                }
                if (read == 0)
                    break; // disconnect

                byte cmd = cmdBuf[0];
                switch (cmd)
                {
                    case (byte)'K':
                        SendScopehalOld(client, cancelTokenSource.Token);
                        break;
                    case (byte)'S':
                        var sw = Stopwatch.StartNew();
                        SendScopehal(client, cancelTokenSource.Token);
                        sw.Stop();
                        logger.LogDebug($"SendScopehal() - {sw.Elapsed.TotalMicroseconds} us");
                        break;
                    default:
                        // Ignore unknown command
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug($"Session exception: {ex.Message}");
        }
        finally
        {

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
                }
                else
                    noCapturesAvailable = true;
            }
            if (noCapturesAvailable)
                Thread.Sleep(10);
        }
    }
}
