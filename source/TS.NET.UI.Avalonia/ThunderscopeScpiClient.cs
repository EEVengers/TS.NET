using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TcpClient = NetCoreServer.TcpClient;

namespace TS.NET.UI.Avalonia
{
    public class ThunderscopeScpiClient : TcpClient
    {
        public ThunderscopeScpiClient(string address, int port) : base(address, port) { }

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Thunderscope SCPI client connected a new session with ID {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Thunderscope SCPI client disconnected a session with ID {Id}");

            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Thunderscope SCPI client caught an error with code {error}");
        }

        private bool _stop;
    }
}
