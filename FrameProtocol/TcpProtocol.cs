using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public class TcpProtocol : FrameProtocol
    {
        private readonly Socket _socket;
        private readonly byte[] _packetHead = new byte[PacketLengthSize];
        private readonly ArrayPool<byte> allocator = ArrayPool<byte>.Shared;

        public TcpProtocol(Socket socket)
        {
            _socket = socket;
        }

        private async Task readFull(byte[] buffer, int offset, int count, CancellationToken cancellation)
        {
            var recvBuf = new ArraySegment<byte>(buffer, offset, count);
            while (count > 0)
            {
                var n = await _socket.ReceiveAsync(recvBuf, SocketFlags.None);
                if (n == 0)
                {
                    throw new Exception("Remote Close the Socket");
                }
                offset += n;
                count -= n;
                recvBuf = recvBuf.Slice(n);
            }
        }

        public async Task<byte[]> ReadAsync(CancellationToken cancellation)
        {
            while (true)
            {
                await readFull(_packetHead, 0, PacketLengthSize, cancellation);
                var bodyLen = BinaryPrimitives.ReadUInt32LittleEndian(_packetHead);
                var packetBody = new byte[bodyLen];
                await readFull(packetBody, 0, (int)bodyLen, cancellation);
                return packetBody;
            }
        }

        public void Write(byte[] data, CancellationToken cancellation = default)
        {
            int bodyLen = data.Length;
            if (bodyLen == 0 || bodyLen > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }

            int totalSize = PacketLengthSize + bodyLen;
            var buffer = allocator.Rent(totalSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)bodyLen);
            data.CopyTo(buffer, PacketLengthSize);

            try
            {
                _socket.BeginSend(buffer, 0, totalSize, SocketFlags.None, (IAsyncResult ar) =>
                {
                    try
                    {
                        _socket.EndSend(ar);
                    }
                    catch (Exception)
                    {
                        _socket.Close();
                    }
                    finally
                    {
                        allocator.Return(buffer);
                    }
                }, null);
            }
            catch (Exception)
            {
                _socket.Close();
            }
        }
    }
}
