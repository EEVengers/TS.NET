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

    private Socket? listener;
    private WaveformSession? currentSession;
    private readonly object sessionLock = new();

    private readonly IPAddress address;
    private readonly int port;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskListener;

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
        listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(address, port));
        listener.Listen(backlog: 1);
        taskListener = Task.Factory.StartNew(() => ListenLoop(logger, listener, cancelTokenSource.Token), TaskCreationOptions.LongRunning);

        startSemaphore.Release();
    }

    public void Stop()
    {
        cancelTokenSource?.Cancel();
        taskListener?.Wait();
        try
        {
            listener?.Close();
        }
        catch { }

        lock (sessionLock)
            currentSession?.Stop();

        lock (sessionLock)
        {
            currentSession?.Join();
            currentSession = null;
        }
    }

    private void ListenLoop(ILogger logger, Socket listener, CancellationToken cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            try
            {
                var client = listener!.Accept();
                logger.LogDebug($"Client accepted: {client.RemoteEndPoint}");
                lock (sessionLock)
                {
                    if (currentSession is { IsRunning: true })
                    {
                        logger.LogDebug("A session is already active; rejecting new connection");
                        try { client.Shutdown(SocketShutdown.Both); } catch { }
                        try { client.Close(); } catch { }
                        continue;
                    }

                    var session = new WaveformSession(client, logger, captureBuffer, cancelTokenSource.Token, OnSessionClosed);
                    currentSession = session;
                    session.Start();
                }
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
    }

    private void OnSessionClosed(WaveformSession session)
    {
        lock (sessionLock)
        {
            if (ReferenceEquals(currentSession, session))
                currentSession = null;
        }
    }
}

internal class WaveformSession
{
    private readonly ILogger logger;
    private readonly ICaptureBufferReader captureBuffer;
    private readonly CancellationToken cancellationToken;
    private readonly Socket socket;
    private readonly Thread thread;
    private readonly Action<WaveformSession> onClose;
    private uint sequenceNumber = 0;

    public bool IsRunning { get; private set; }

    public WaveformSession(Socket socket, ILogger logger, ICaptureBufferReader captureBuffer, CancellationToken cancellationToken, Action<WaveformSession> onClose)
    {
        this.socket = socket;
        this.logger = logger;
        this.captureBuffer = captureBuffer;
        this.cancellationToken = cancellationToken;
        this.onClose = onClose;
        thread = new Thread(Run) { IsBackground = true, Name = $"WaveformSession-{socket.GetHashCode()}" };
    }

    public void Start()
    {
        IsRunning = true;
        logger.LogDebug($"Waveform session started ({socket.RemoteEndPoint})");
        thread.Start();
    }

    public void Stop()
    {
        try { socket.Shutdown(SocketShutdown.Both); } catch { }
        try { socket.Close(); } catch { }
    }

    public void Join() => thread.Join();

    private void Run()
    {
        Span<byte> cmdBuf = stackalloc byte[1];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = 0;
                try
                {
                    read = socket.Receive(cmdBuf);
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
                        SendScopehalOld();
                        break;
                    case (byte)'S':
                        var sw = Stopwatch.StartNew();
                        SendScopehal();
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
            IsRunning = false;
            onClose(this);
            Stop();
            logger.LogDebug("Waveform session ended");
        }
    }

    private void SendAll(ReadOnlySpan<byte> data)
    {
        while (!data.IsEmpty)
        {
            int sent = 0;
            try
            {
                sent = socket.Send(data);
            }
            catch (SocketException se)
            {
                logger.LogDebug($"Send error {se.SocketErrorCode}");
                throw;
            }
            data = data[sent..];
        }
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
                        SendAll(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeaderOld)));
                        for (byte captureBufferIndex = 0; captureBufferIndex < captureBuffer.ChannelCount; captureBufferIndex++)
                        {
                            int channelIndex = captureMetadata.HardwareConfig.GetChannelIndexByCaptureBufferIndex(captureBufferIndex);
                            ThunderscopeChannelFrontend thunderscopeChannel = captureMetadata.HardwareConfig.Frontend[channelIndex];
                            chHeader.channelIndex = (byte)channelIndex;
                            chHeader.scale = (float)(thunderscopeChannel.ActualVoltFullScale / 256.0);
                            chHeader.offset = (float)thunderscopeChannel.ActualVoltOffset;
                            SendAll(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeaderOld)));
                            var channelBuffer = MemoryMarshal.Cast<sbyte, byte>(captureBuffer.GetChannelReadBuffer<sbyte>(captureBufferIndex));
                            SendAll(channelBuffer);
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

    private void SendScopehal()
    {
        bool noCapturesAvailable = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                        SendAll(new ReadOnlySpan<byte>(&header, sizeof(WaveformHeader)));
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
                            SendAll(new ReadOnlySpan<byte>(&chHeader, sizeof(ChannelHeader)));
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
                            SendAll(channelBuffer);
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
