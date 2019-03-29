using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public class PipeProtocol : FrameProtocol
    {
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;

        public PipeProtocol(IDuplexPipe pipe)
        {
            _reader = pipe.Input;
            _writer = pipe.Output;
        }

        public PipeProtocol(PipeReader reader, PipeWriter writer)
        {
            _reader = reader;
            _writer = writer;
        }

        private bool TryReadPacketBodyLen(in ReadOnlySequence<byte> buffer, out uint packetLength)
        {
            if (buffer.Length < PacketLengthSize)
            {
                packetLength = 0;
                return false;
            }

            Span<byte> span = stackalloc byte[PacketLengthSize];
            buffer.Slice(0, PacketLengthSize).CopyTo(span);
            packetLength = BinaryPrimitives.ReadUInt32LittleEndian(span);

            if (packetLength == 0 || packetLength > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }
            return true;
        }

        public override async Task<(IMemoryOwner<byte>, uint)> ReadAsync(CancellationToken cancellation = default)
        {
            while (true)
            {
                ReadResult result = await _reader.ReadAsync(cancellation);
                if (result.IsCompleted || result.IsCanceled)
                {
                    return (null, 0);
                }
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (!TryReadPacketBodyLen(in buffer, out uint bodyLen) || buffer.Length < bodyLen + PacketLengthSize)
                {
                    _reader.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                ReadOnlySequence<byte> body = buffer.Slice(PacketLengthSize, bodyLen);
                IMemoryOwner<byte> buf = Pipelines.Sockets.Unofficial.Arenas.ArrayPoolAllocator<byte>.Shared.Allocate((int)bodyLen);
                body.CopyTo(buf.Memory.Span);
                _reader.AdvanceTo(body.End);
                return (buf, bodyLen);
            }
        }

        public Task WriteAsync(ReadOnlyMemory<byte> head, ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            WriteBuffer(in head, in data);
            ValueTask<FlushResult> result = _writer.FlushAsync(cancellation);
            return result.AsTask();
        }

        public override Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            WriteBuffer(in data);
            ValueTask<FlushResult> result = _writer.FlushAsync(cancellation);
            return result.AsTask();
        }

        private void WriteBuffer(in ReadOnlyMemory<byte> head, in ReadOnlyMemory<byte> data)
        {
            int len = head.Length + data.Length;
            if (len == 0 || len > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }

            int totalSize = PacketLengthSize + len;

            Memory<byte> buffer = _writer.GetMemory(totalSize);

            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Span, (uint)len);

            head.CopyTo(buffer.Slice(PacketLengthSize));
            data.CopyTo(buffer.Slice(PacketLengthSize + head.Length));

            _writer.Advance(totalSize);
        }

        private void WriteBuffer(in ReadOnlyMemory<byte> data)
        {
            int bodyLen = data.Length;
            if (bodyLen == 0 || bodyLen > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }

            int totalSize = PacketLengthSize + bodyLen;

            Memory<byte> buffer = _writer.GetMemory(totalSize);

            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Span, (uint)bodyLen);

            data.CopyTo(buffer.Slice(PacketLengthSize));

            _writer.Advance(totalSize);
        }
    }
}
