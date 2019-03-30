using System;
using FrameProtocol;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;

namespace EchoServer
{
    public class PipeEchoServer : SocketServer
    {
        protected override Task OnClientConnectedAsync(in ClientConnection client)
        {
            return Echo(client.Transport);
        }

        private async Task Echo(IDuplexPipe transport)
        {
            PipeFrameProtocol protocol = new PipeFrameProtocol(transport);
            try
            {
                while (true)
                {
                    ReadOnlyMemory<byte> packet = await protocol.ReadAsync();
                    if (packet.Length == 0)
                    {
                        return;
                    }
                    await protocol.WriteAsync(packet);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                transport.Output.Complete();
            }

        }
    }
}
