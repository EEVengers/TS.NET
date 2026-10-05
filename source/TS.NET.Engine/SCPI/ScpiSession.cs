using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TS.NET.Engine;

internal class ScpiSession
{
    private readonly Socket socket;
    private readonly ILogger logger;
    private readonly ScpiHandler handler;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskLoop;

    public EndPoint? RemoteEndpoint { get; }
    public bool IsActive => taskLoop != null && !taskLoop.IsCompleted;

    public ScpiSession(Socket socket, ILogger logger, ScpiHandler handler)
    {
        this.socket = socket;
        this.logger = logger;
        this.handler = handler;
        RemoteEndpoint = socket.RemoteEndPoint;
    }

    public void Start()
    {
        cancelTokenSource = new CancellationTokenSource();
        taskLoop = Task.Factory.StartNew(() => Loop(cancelTokenSource.Token), TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        cancelTokenSource?.Cancel();
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException ex)
        {
            logger.LogDebug(ex, "SCPI session socket shutdown failed");
        }
        catch (ObjectDisposedException)
        {
            logger.LogDebug("SCPI session socket is already closed");
        }
        socket.Dispose();
        taskLoop?.Wait();
        cancelTokenSource = null;
        taskLoop = null;
    }

    private void Loop(CancellationToken cancellationToken)
    {
        string sessionId = RemoteEndpoint?.ToString() ?? "Unknown";
        var buffer = new byte[4096];
        var sb = new StringBuilder();
        int scanIndex = 0;

        try
        {
            logger.LogDebug($"SCPI session accepted ({sessionId})");
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = socket.Receive(buffer);
                if (read == 0)
                    break;

                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                while (scanIndex < sb.Length)
                {
                    if (sb[scanIndex] != '\n')
                    {
                        scanIndex++;
                        continue;
                    }

                    string message = sb.ToString(0, scanIndex).TrimEnd('\r');
                    sb.Remove(0, scanIndex + 1);
                    scanIndex = 0;
                    cancellationToken.ThrowIfCancellationRequested();
                    string? response = handler.ProcessSCPICommand(message, cancellationToken);
                    if (response == null)
                        continue;

                    logger.LogDebug($"SCPI response: {response.TrimEnd()}");
                    byte[] bytes = Encoding.ASCII.GetBytes(response);
                    int sent = 0;
                    while (sent < bytes.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int count = socket.Send(bytes.AsSpan(sent), SocketFlags.None);
                        if (count == 0)
                            throw new IOException("SCPI socket closed while sending a response.");
                        sent += count;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException ex)
        {
            logger.LogDebug(ex, $"SCPI socket disconnected ({sessionId})");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"SCPI session failed ({sessionId})");
        }
        finally
        {
            socket.Dispose();
            logger.LogDebug($"SCPI session dropped ({sessionId})");
        }
    }
}
