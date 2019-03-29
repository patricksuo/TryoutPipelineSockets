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
        private CancellationTokenSource cancel = new CancellationTokenSource();

        public void Listen(IPEndPoint ep, int backlog = 10000)
        {
            lisenter = new TcpListener(ep);
            lisenter.Start(backlog);
            acceptLoop();
        }

        public void Stop()
        {
            cancel.Cancel();
        }

        private void acceptLoop()
        {
            Task.Run(async () =>
            {
                while (!cancel.IsCancellationRequested)
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
                var protocol = new TcpProtocol(client.Client);
                try
                {
                    while (true)
                    {
                        var buffer = await protocol.ReadAsync(cancel.Token);
                        protocol.Write(buffer);
                    }
                }
                catch (Exception) { }
            });
        }
    }
}
