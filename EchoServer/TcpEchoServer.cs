using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using FrameProtocol;

namespace EchoServer
{
    public class TcpEchoServer
    {
        private TcpListener lisenter;
        private CancellationTokenSource _cancel = new CancellationTokenSource();

        public void Listen(IPEndPoint ep, int backlog = 10000)
        {
            lisenter = new TcpListener(ep);
            lisenter.Start(backlog);
            acceptLoop();
        }

        public void Stop()
        {
            _cancel.Cancel();
        }

        private void acceptLoop()
        {
            Task.Run(async () =>
            {
                while (!_cancel.IsCancellationRequested)
                {
                    var client = await lisenter.AcceptTcpClientAsync();
                    OnClientConnectedAsync(client);
                }
            });
        }

        private void OnClientConnectedAsync(TcpClient client)
        {
            Task.Run(async () =>
            {
                var protocol = new TcpFrameProtocol(client.Client);
                try
                {
                    while (true)
                    {
                        ReadOnlyMemory<byte> packet = await protocol.ReadAsync(_cancel.Token);
                        await protocol.WriteAsync(packet);
                    }
                }
                catch (Exception) { }
            });
        }
    }
}
