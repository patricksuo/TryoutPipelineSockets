using CommandLine;
using System;
using System.Net;
using RuntimeTracing;

namespace EchoServer
{
    public enum TestType
    {
        Pipeline,
        TcpSocket,
    }

    public class Options
    {
        [Option('h', "help", Required = false, Default = false, HelpText = "print usage info and exit")]
        public bool Help { get; set; }

        [Option('l', "address", Required = false, Default = "0.0.0.0", HelpText = "listening address")]
        public string Address { get; set; }

        [Option('p', "port", Required = false, Default = 10008, HelpText = "listening port")]
        public int Port { get; set; }

        [Option('t', "type", Default = TestType.Pipeline, HelpText = "test type")]
        public TestType testType { get; set; }

        [Option('v', "verbose", Required = false, Default = false, HelpText = "print verbose tracing log")]
        public bool Verbose { get; set; }

        public static Options s_Current;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Options options = null;
            RuntimeEventListener eventListener = null;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(_options =>
                {
                    options = _options;
                });

            if (options == null)
            {
                return;
            }
            if (options.Verbose)
            {
                eventListener = new RuntimeEventListener();
            }

            IPAddress address = IPAddress.Parse(options.Address);
            IPEndPoint endpoint = new IPEndPoint(address, options.Port);

            switch (options.testType)
            {
                case TestType.Pipeline:
                    {
                        PipeEchoServer server = new PipeEchoServer();
                        server.Listen(endpoint, listenBacklog: 5000);
                    }
                    break;
                case TestType.TcpSocket:
                    {
                        TcpEchoServer server = new TcpEchoServer();
                        server.Listen(endpoint, 5000);
                    }
                    break;
            }

            Console.WriteLine("enter return to exit");
            Console.ReadLine();
        }
    }
}
