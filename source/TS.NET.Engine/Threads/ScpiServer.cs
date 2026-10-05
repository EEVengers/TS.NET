using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace TS.NET.Engine;

internal class ScpiServer : IThread
{
    private readonly ILogger logger;
    private readonly IPEndPoint endpoint;
    private readonly ScpiHandler handler;
    private readonly Lock sessionLock = new();

    private CancellationTokenSource? listenerCancelTokenSource;
    private Task? taskListener;
    private Socket? socketListener;

    private ScpiSession? session;

    public ScpiServer(ILogger logger, ThunderscopeSettings settings, string thunderscopeSerial, IPEndPoint endpoint,
        BlockingRequestResponse<ProcessingRequest, ProcessingResponse> processingControl)
    {
        this.logger = logger;
        this.endpoint = endpoint;
        handler = new ScpiHandler(logger, settings, thunderscopeSerial, processingControl);
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.Bind(endpoint);
            socket.Listen(backlog: 1);
        }
        catch (SocketException ex)
        {
            socket.Dispose();
            logger.LogError(ex, $"Unable to start SCPI listener at {endpoint}");
            throw;
        }

        socketListener = socket;
        listenerCancelTokenSource = new CancellationTokenSource();
        taskListener = Task.Factory.StartNew(() => LoopListener(socket, listenerCancelTokenSource.Token), TaskCreationOptions.LongRunning);
        logger.LogDebug($"SCPI socket listening {socket.LocalEndPoint}");
        startSemaphore.Release();
    }

    public void Stop()
    {
        listenerCancelTokenSource?.Cancel();
        socketListener?.Dispose();
        lock (sessionLock)
        {
            session?.Stop();
            session = null;
        }

        taskListener?.Wait();
        listenerCancelTokenSource?.Dispose();
        listenerCancelTokenSource = null;
        taskListener = null;
        socketListener = null;
    }

    public void OnUpdateSequence(uint seq)
    {
        handler.OnUpdateSequence(seq);
    }

    private void LoopListener(Socket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var acceptedSocket = socket.Accept();
                lock (sessionLock)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        acceptedSocket.Dispose();
                        break;
                    }

                    if (session?.IsActive == true)
                        logger.LogDebug($"Dropping SCPI session {session.RemoteEndpoint} and accepting new SCPI session");

                    // Finish the previous session before resetting the shared sequence state.
                    session?.Stop();
                    acceptedSocket.NoDelay = true;
                    handler.OnUpdateSequence(0);
                    session = new ScpiSession(acceptedSocket, logger, handler);
                    session.Start();
                }
            }
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "SCPI listener failed");
        }
        finally
        {
            socket.Dispose();
            logger.LogDebug("SCPI socket closed");
        }
    }
}
