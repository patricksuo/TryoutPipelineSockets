using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public class TcpFrameProtocol : FrameProtocol
    {
        private readonly Socket _socket;
        private readonly Memory<byte> _headBuffer = new Memory<byte>( new byte[PacketLengthSize]);
        private Memory<byte> _buffer = new Memory<byte>(new byte[128]);

        public TcpFrameProtocol(Socket socket)
        {
            _socket = socket;
        }

        private async Task ReadFull(Memory<byte> buffer, CancellationToken cancellation)
        {
            var count = buffer.Length;
            while (count > 0)
            {
                var n = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellation);
                if (n == 0)
                {
                    ThrowEOS();
                }
                count -= n;
                buffer = buffer.Slice(n);
            }
        }

        public override async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken token=default)
        {
            while(!token.IsCancellationRequested)
            {
                await ReadFull(_headBuffer, token);
                int bodyLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(_headBuffer.Span);
                if (bodyLen <=0 || bodyLen > MaxPacketSize)
                {
                    ThrowFrameSizeEx();
                }

                if (_buffer.Length < bodyLen)
                {
                    _buffer = new Memory<byte>(new byte[bodyLen]);
                }

                await ReadFull(_buffer.Slice(0, bodyLen), token);
                return _buffer.Slice(0, bodyLen);
            }
            return default;
        }

        public  override async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            int bodyLen = data.Length;
            if (bodyLen == 0 || bodyLen > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }

            int totalSize = PacketLengthSize + bodyLen;
            IMemoryOwner<byte> imo = MemoryPool<byte>.Shared.Rent(totalSize);
            Memory<byte> buffer = imo.Memory.Slice(0, totalSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Span, (uint)bodyLen);
            data.CopyTo(buffer.Slice(PacketLengthSize));

            int sentCount = 0;
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    var n = await _socket.SendAsync(buffer, SocketFlags.None);
                    sentCount += n;
                    if (sentCount == totalSize)
                    {
                        break;
                    }
                    buffer = buffer.Slice(n);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                imo.Dispose();
            }
        }
    }
}
