using CommandLine;
using System;
using Pipelines.Sockets.Unofficial;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Buffers;
using System.Net;

namespace EchoServer
{
    public class Options
    {
        [Option('h', "help", Required = false, Default = false, HelpText = "print usage info and exit")]
        public bool Help { get; set; }

        [Option('l', "address", Required = false, Default = "0.0.0.0", HelpText = "listening address")]
        public string Address { get; set; }

        [Option('p', "port", Required = false, Default = 10008, HelpText = "listening port")]
        public int Port { get; set; }

        public static Options s_Current;
    }

    public class EchoServer : SocketServer
    {
        protected override Task OnClientConnectedAsync(in ClientConnection client)
        {
            return Echo(client.Transport);
        }

        private async Task Echo(IDuplexPipe transport)
        {
            FrameProtocol.FrameProtocol protocol = new FrameProtocol.FrameProtocol(transport);
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
            catch(Exception)
            {

            }
            finally
            {
                transport.Output.Complete();
            }

        }



    }


    class Program
    {
        static void Main(string[] args)
        {
            Options options = null;


            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(_options =>
                {
                    options = _options;
                });

            if (options == null)
            {
                return;
            }

            EchoServer server = new EchoServer();
            IPAddress address = IPAddress.Parse(options.Address);
            EndPoint endpoint = new IPEndPoint(address, options.Port);

            server.Listen(endpoint);


            Console.WriteLine("enter return to exit");
            Console.ReadLine();
            server.Stop();

        }
    }
}
