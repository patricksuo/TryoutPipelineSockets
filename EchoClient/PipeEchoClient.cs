using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FrameProtocol;
using Pipelines.Sockets.Unofficial;

namespace EchoClient
{
    public class PipeEchoClient
    {
        private readonly EndPoint _server;
        private readonly int _echoRound;
        private readonly byte[] _payload;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public TimeSpan ConnectDuration { get; private set; }
        public TimeSpan EchoDuration { get; private set; }
        public Exception Error { get; private set; }

        public static long ConnectBeginCnt;
        public static long ConnectFinishCnt;
        public static long WriteBeginCnt;
        public static long WriteFinishCnt;
        public static long ReadFinishCnt;



        public PipeEchoClient(EndPoint server, int echoRound, byte[] payload)
        {
            _server = server;
            _echoRound = echoRound;
            _payload = payload;
        }

        public async Task Start()
        {
            _stopwatch.Start();
            Interlocked.Increment(ref ConnectBeginCnt);
            SocketConnection conn = await SocketConnection.ConnectAsync(_server);
            Interlocked.Increment(ref ConnectFinishCnt);
            ConnectDuration = _stopwatch.Elapsed;

            _stopwatch.Restart();
            try
            {
                var protocol = new PipeProtocol(conn.Input, conn.Output);

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
                conn.Dispose();
            }
        }
    }
}
