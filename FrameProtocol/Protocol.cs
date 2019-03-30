using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public abstract class FrameProtocol
    {
        public const int PacketLengthSize = sizeof(uint);
        public const int MaxPacketSize = int.MaxValue;

        private readonly static Exception s_frameSizeException = new Exception("FrameSizeException");
        private readonly static EndOfStreamException s_endOfStreamException = new EndOfStreamException();


        protected static void ThrowFrameSizeEx()
        {
            throw s_frameSizeException;
        }

        protected static void ThrowEOS()
        {
            throw s_endOfStreamException;
        }

        public abstract Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken token = default);
        public abstract Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation = default);
    }
}
