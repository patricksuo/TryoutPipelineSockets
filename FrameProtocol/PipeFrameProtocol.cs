using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace FrameProtocol
{
    public class PipeFrameProtocol : FrameProtocol
    {
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;

        private byte[] _array;
        private Memory<byte> _buffer;

        public PipeFrameProtocol(IDuplexPipe pipe)
        {
            _reader = pipe.Input;
            _writer = pipe.Output;
        }

        public PipeFrameProtocol(PipeReader reader, PipeWriter writer)
        {
            _reader = reader;
            _writer = writer;

            _array = new byte[128];
            _buffer = new Memory<byte>(_array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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


        public override async Task<ReadOnlyMemory<byte>> ReadAsync(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                ReadResult result = await _reader.ReadAsync(token);
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
                if (_buffer.Length < (int)body.Length)
                {
                    _array = new byte[(int)body.Length];
                    _buffer = new Memory<byte>(_array);
                }
                body.CopyTo(_buffer.Span.Slice(0, (int)bodyLen));
                _reader.AdvanceTo(body.End);
                return _buffer.Slice(0, (int)bodyLen);
            }
            return default;
        }


        public override Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token = default)
        {
            if (data.Length <= 0 || data.Length > MaxPacketSize)
            {
                ThrowFrameSizeEx();
            }
            int totalSize = PacketLengthSize + data.Length;
            Memory<byte> buffer = _writer.GetMemory(totalSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Span, (uint)data.Length);
            data.CopyTo(buffer.Slice(PacketLengthSize, data.Length));
            _writer.Advance(totalSize);
            ValueTask<FlushResult> result = _writer.FlushAsync(token);
            return result.AsTask();
        }


    }
}
