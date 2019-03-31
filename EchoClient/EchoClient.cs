using System;
using System.Diagnostics;
using System.IO.Pipelines;
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

        private FrameProtocol.FrameProtocol _protocol;

        public TimeSpan ConnectDuration { get; private set; }
        public TimeSpan EchoDuration { get; private set; }
        public Exception Error { get; private set; }

        public static long s_connectBeginCnt;
        public static long s_connectFinishCnt;
        public static long s_writeBeginCnt;
        public static long s_writeFinishCnt;
        public static long s_readFinishCnt;

        private static readonly PipeOptions s_pipeOptions = 
        new PipeOptions(readerScheduler: PipeScheduler.Inline,
            writerScheduler: PipeScheduler.Inline,
            minimumSegmentSize: 512
                );

        public EchoClient(EndPoint server, int echoRound, byte[] payload)
        {
            _server = server;
            _echoRound = echoRound;
            _payload = payload;
        }

        public async Task Start(TestType testType)
        {
            Interlocked.Increment(ref s_connectBeginCnt);
            SocketConnection conn = null;
            TcpClient client = null;

            switch (testType)
            {
                case TestType.Pipeline:
                    _stopwatch.Start();
                    conn = await SocketConnection.ConnectAsync(_server,pipeOptions: s_pipeOptions);
                    _protocol = new PipeFrameProtocol(conn.Input, conn.Output);
                    break;
                case TestType.TcpSocket:
                    client = new TcpClient();
                    _stopwatch.Start();
                    await client.ConnectAsync(((IPEndPoint)_server).Address, ((IPEndPoint)_server).Port);
                    _protocol = new TcpFrameProtocol(client.Client);
                    break;
            }

            Interlocked.Increment(ref s_connectFinishCnt);
            ConnectDuration = _stopwatch.Elapsed;

            _stopwatch.Restart();
            try
            {
                for (int i = 0; i < _echoRound; i++)
                {
                    Interlocked.Increment(ref s_writeBeginCnt);
                    await _protocol.WriteAsync(_payload);
                    Interlocked.Increment(ref s_writeFinishCnt);
                    ReadOnlyMemory<byte> buffer = await _protocol.ReadAsync();
                    if (buffer.Length == 0)
                    {
                        return;
                    }
                    Interlocked.Increment(ref s_readFinishCnt);
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
