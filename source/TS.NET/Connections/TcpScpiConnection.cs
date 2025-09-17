using System.Net.Sockets;
using System.Text;

namespace TS.NET;

public class TcpScpiConnection : IDisposable
{
    private TcpClient? tcpClient;
    internal NetworkStream? networkStream;
    private StreamWriter? streamWriter;
    private StreamReader? streamReader;

    public string WriteTermination { get; set; } = "\n";
    public bool IsOpen => tcpClient?.Connected ?? false;

    private int readTimeoutMs = 3000;
    public int ReadTimeoutMs
    {
        get => readTimeoutMs;
        set
        {
            readTimeoutMs = value;
            if (networkStream != null)
                networkStream.ReadTimeout = value;
        }
    }

    private int writeTimeoutMs = 3000;
    public int WriteTimeoutMs
    {
        get => writeTimeoutMs;
        set
        {
            writeTimeoutMs = value;
            if (networkStream != null)
                networkStream.WriteTimeout = value;
        }
    }

    public int ConnectTimeoutMs { get; set; } = 3000;

    public void Open(string ipAddress, int port)
    {
        if (IsOpen)
            Close();

        tcpClient = new TcpClient();
        IAsyncResult result = tcpClient.BeginConnect(ipAddress, port, null, null);

        if (!result.AsyncWaitHandle.WaitOne(ConnectTimeoutMs, true))
        {
            tcpClient.Close();
            throw new TimeoutException($"Connection to {ipAddress}:{port} timed out.");
        }

        tcpClient.EndConnect(result);

        networkStream = tcpClient.GetStream();
        networkStream.ReadTimeout = ReadTimeoutMs;
        networkStream.WriteTimeout = WriteTimeoutMs;

        var encoding = Encoding.ASCII;
        streamWriter = new StreamWriter(networkStream, encoding, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = WriteTermination,
            AutoFlush = true
        };
        streamReader = new StreamReader(networkStream, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        streamReader?.Dispose();
        streamWriter?.Dispose();
        networkStream?.Dispose();
        tcpClient?.Close();

        streamReader = null;
        streamWriter = null;
        networkStream = null;
        tcpClient = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }
    }

    public void WriteLine(string data)
    {
        CheckOpen();
        streamWriter!.WriteLine(data);
    }

    public string ReadLine()
    {
        CheckOpen();
        return streamReader!.ReadLine() ?? string.Empty;
    }

    internal void CheckOpen()
    {
        if (!IsOpen)
            throw new InvalidOperationException("Connection is not open.");
    }
}
