using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FrameProtocol;
using Pipelines.Sockets.Unofficial;

namespace EchoClient
{
    public class EchoClient
    {
        private readonly EndPoint _server;
        private readonly int _echoRound;
        private readonly byte[] _payload;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private FrameProtocol.FrameProtocol protocol;

        public TimeSpan ConnectDuration { get; private set; }
        public TimeSpan EchoDuration { get; private set; }
        public Exception Error { get; private set; }

        public static long ConnectBeginCnt;
        public static long ConnectFinishCnt;
        public static long WriteBeginCnt;
        public static long WriteFinishCnt;
        public static long ReadFinishCnt;

        public EchoClient(EndPoint server, int echoRound, byte[] payload)
        {
            _server = server;
            _echoRound = echoRound;
            _payload = payload;
        }

        public async Task Start(TestType testType)
        {
            _stopwatch.Start();
            Interlocked.Increment(ref ConnectBeginCnt);
            SocketConnection conn = null;
            TcpClient client = null;

            switch (testType)
            {
                case TestType.Pipeline:
                    conn = await SocketConnection.ConnectAsync(_server);
                    protocol = new PipeProtocol(conn.Input, conn.Output);
                    break;
                case TestType.TcpSocket:
                    await client.ConnectAsync(((IPEndPoint)_server).Address, ((IPEndPoint)_server).Port);
                    protocol = new TcpProtocol(client.Client);
                    break;
            }

            Interlocked.Increment(ref ConnectFinishCnt);
            ConnectDuration = _stopwatch.Elapsed;

            _stopwatch.Restart();
            try
            {
                for (int i = 0; i < _echoRound; i++)
                {
                    Interlocked.Increment(ref WriteBeginCnt);
                    await protocol.WriteAsync(_payload);
                    Interlocked.Increment(ref WriteFinishCnt);
                    (var imo, var len) = await protocol.ReadAsync();
                    Interlocked.Increment(ref ReadFinishCnt);
                    using (imo)
                    {
                        if (len != _payload.Length)
                        {
                            throw new Exception("unexpect echo result");
                        }
                    }
                }
                EchoDuration = _stopwatch.Elapsed;
            }
            catch (Exception e)
            {
                Error = e;
            }
            finally
            {
                conn?.Dispose();
                client?.Dispose();
            }
        }
    }
}
