using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public class TcpProtocol : FrameProtocol
    {
        private readonly Socket _socket;
        private readonly MemoryPool<byte> allocator = MemoryPool<byte>.Shared;

        public TcpProtocol(Socket socket)
        {
            _socket = socket;
        }

        private async Task readFull(Memory<byte> buffer, CancellationToken cancellation)
        {
            var count = buffer.Length;
            while (count > 0)
            {
                var n = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellation);
                if (n == 0)
                {
                    throw new Exception("Remote Close the Socket");
                }
                count -= n;
                buffer = buffer.Slice(n);
            }
        }

        public override async Task<(IMemoryOwner<byte>, uint)> ReadAsync(CancellationToken cancellation)
        {
            while (true)
            {
                var headBuf = allocator.Rent(PacketLengthSize);
                var head = headBuf.Memory.Slice(0, PacketLengthSize);
                await readFull(head, cancellation);
                var bodyLen = BinaryPrimitives.ReadUInt32LittleEndian(head.Span);
                headBuf.Dispose();

                var bodyBuf = MemoryPool<byte>.Shared.Rent((int)bodyLen);
                var body = bodyBuf.Memory.Slice(0, (int)bodyLen);
                await readFull(body, cancellation);
                return (bodyBuf, bodyLen);
            }
        }

        public override async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            int bodyLen = data.Length;
            if (bodyLen == 0 || bodyLen > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }

            int totalSize = PacketLengthSize + bodyLen;
            var imo = MemoryPool<byte>.Shared.Rent(totalSize);
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
