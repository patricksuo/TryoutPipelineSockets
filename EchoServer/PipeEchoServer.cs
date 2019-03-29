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
            var protocol = new PipeProtocol(transport);
            try
            {
                while (true)
                {
                    (var buffer, var len) = await protocol.ReadAsync();
                    if (len == 0)
                    {
                        return;
                    }
                    using (buffer)
                    {
                        await protocol.WriteAsync(buffer.Memory.Slice(0, (int)len));
                    }
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
