using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public class PipeFrameProtocol : FrameProtocol
    {
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;
        private readonly byte[] _headBuffer =new byte[PacketLengthSize];

        private Memory<byte> _buffer = new Memory<byte>(new byte[128]);

        public PipeFrameProtocol(IDuplexPipe pipe)
        {
            _reader = pipe.Input;
            _writer = pipe.Output;
        }

        public PipeFrameProtocol(PipeReader reader, PipeWriter writer)
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


        public override async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellation = default)
        {
            while (!cancellation.IsCancellationRequested)
            {
                ReadResult result = await _reader.ReadAsync(cancellation);
                if (result.IsCompleted || result.IsCanceled)
                {
                    return default;
                }
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (!TryReadPacketBodyLen(in buffer, out uint bodyLen) || buffer.Length < bodyLen + PacketLengthSize)
                {
                    _reader.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                ReadOnlySequence<byte> body = buffer.Slice(PacketLengthSize, bodyLen);
                IMemoryOwner<byte> buf = MemoryPool<byte>.Shared.Rent((int)bodyLen);
                body.CopyTo(_buffer.Span.Slice(0,(int)bodyLen));
                _reader.AdvanceTo(body.End);
                return _buffer.Slice(0, (int)bodyLen);
            }
            return default;
        }

        public override Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellation = default)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_headBuffer, (uint)data.Length);
            ReadOnlyMemory<byte> head = _headBuffer;
            WriteBuffer(in head, in data);
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
