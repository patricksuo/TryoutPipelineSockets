using System;
using System.Collections.Generic;
using System.Text;

namespace EchoClient
{
    public class TcpEchoClient
    {
        private readonly EndPoint _server;
        private readonly int _echoRound;
        private readonly byte[] _payload;
        private readonly Stopwatch _stopwatch = new Stopwatch();
    }
}
