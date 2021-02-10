using Benchmark.Common;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Benchmark.Other
{
    internal class SerializersBenchmark
    {
        public void Run()
        {
            var rnd = new Random();
            var entityCount = 1000000;

            ProtoBuf.Serializer.PrepareSerializer<FooEntity>();

            var generator = new EntityGenerator();

            Measure("memStream-proto", entityCount, () =>
            {
                using (var memStream = new MemoryStream())
                {
                    for (int i = 0; i < entityCount; i++)
                    {
                        var entity = generator.Next();
                        ProtoBuf.Serializer.Serialize(memStream, entity);
                    }

                    return memStream.Position;
                }
            });

            Measure("memStream-msgpack", entityCount, () =>
            {
                using (var memStream = new MemoryStream())
                {
                    for (int i = 0; i < entityCount; i++)
                    {
                        var entity = generator.Next();
                        MessagePack.MessagePackSerializer.Serialize(memStream, entity);
                    }

                    return memStream.Position;
                }
            });

            Measure("buffer-proto", entityCount, () =>
            {
                var buffer = new TxBuffer(1024 * 50);

                for (int i = 0; i < entityCount; i++)
                {
                    var entity = generator.Next();
                    ProtoBuf.Serializer.Serialize((IBufferWriter<byte>)buffer, entity);
                }

                return buffer.Size;
            });

            Measure("buffer-msgpack", entityCount, () =>
            {
                //var options = new MessagePack.MessagePackSerializerOptions();
                var buffer = new TxBuffer(1024 * 50);

                for (int i = 0; i < entityCount; i++)
                {
                    var entity = generator.Next();
                    MessagePack.MessagePackSerializer.Serialize((IBufferWriter<byte>)buffer, entity);
                }

                return buffer.Size;
            });


            //Measure("stream", entityCount, () =>
            //{
            //    using (var stream = new SomeBuffer(1024 * 50))
            //    {
            //        for (int i = 0; i < entityCount; i++)
            //        {
            //            ProtoBuf.Serializer.Serialize(stream, entity);
            //        }
            //    }
            //});


            Console.WriteLine("Done");
            Console.Read();
        }

        private static void Measure(string name, int count, Func<long> toMeasure)
        {
            var watch = Stopwatch.StartNew();
            var totalSize = (double)toMeasure();
            watch.Stop();

            var speed = count / watch.Elapsed.TotalSeconds;
            var avgSize = totalSize / count;

            Console.WriteLine("{0} - {1:f1} eps, avg.size - {2:f1} bytes", name, speed, avgSize);
        }

        public class TxBuffer : Stream, System.Buffers.IBufferWriter<byte>
        {
            private readonly List<ArraySegment<byte>> _completeSegments = new List<ArraySegment<byte>>();
            private readonly int _segmentSize = 1024;
            private readonly int _minSizeHint = 128;
            private byte[] _currentSegment;
            private int _currentOffset;

            public TxBuffer(int segmentSize)
            {
                _segmentSize = segmentSize;
                _currentSegment = new byte[_segmentSize];
            }

            public long Size { get; protected set; }

            public void Advance(int count)
            {
                _currentOffset += count;
                Size += count;
            }

            public Memory<byte> GetMemory(int sizeHint = 0)
            {
                EnsureSpace(sizeHint);
                return new Memory<byte>(_currentSegment, _currentOffset, _segmentSize - _currentOffset);
            }

            public Span<byte> GetSpan(int sizeHint = 0)
            {
                EnsureSpace(sizeHint);
                return new Span<byte>(_currentSegment, _currentOffset, _segmentSize - _currentOffset);
            }

            private void EnsureSpace(int sizeHint)
            {
                if (sizeHint <= _minSizeHint)
                    sizeHint = _minSizeHint;

                var spaceInCurrentSegment = _segmentSize - _currentOffset;

                if (spaceInCurrentSegment < sizeHint)
                {
                    _completeSegments.Add(new ArraySegment<byte>(_currentSegment, 0, _currentOffset));
                    _currentSegment = new byte[_segmentSize];
                    _currentOffset = 0;
                }
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotImplementedException();
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush() => throw new NotImplementedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
            public override void SetLength(long value) => throw new NotImplementedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    var space = _segmentSize - _currentOffset;
                    var toCopy = Math.Min(count, space);

                    Array.Copy(buffer, offset, _currentSegment, _currentOffset, toCopy);

                    count -= toCopy;
                    _currentOffset += toCopy;

                    if (_currentOffset >= _segmentSize)
                    {
                        _completeSegments.Add(new ArraySegment<byte>(_currentSegment, 0, _currentOffset));
                        _currentSegment = new byte[_segmentSize];
                        _currentOffset = 0;
                    }
                }
            }
        }
    }
}
