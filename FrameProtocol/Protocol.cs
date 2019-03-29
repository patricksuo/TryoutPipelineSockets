using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FrameProtocol
{
    public abstract class FrameProtocol
    {
        public const int PacketLengthSize = sizeof(uint);
        public const int MaxPacketSize = int.MaxValue;

        protected static void ThrowFrameSizeEx()
        {
            throw new Exception("FrameSizeException");
        }

        public abstract Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation = default);

        public abstract Task<(IMemoryOwner<byte>, uint)> ReadAsync(CancellationToken cancellation = default);
    }
}
