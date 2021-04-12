using SharpRpc.Lib;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class RxBuffer
    {
        private const int MaxThreshold = 1024 * 5;

        private Queue<RxSegment> _tail = new Queue<RxSegment>();
        private RxSegment _currentSegment;
        private readonly int _segmentSize;
        private readonly int _segmentSizeThreshold;

        public RxBuffer(int segmentSize)
        {
            _segmentSize = segmentSize;
            _segmentSizeThreshold = (int)(segmentSize * 0.3);

            if (_segmentSizeThreshold > MaxThreshold)
                _segmentSizeThreshold = MaxThreshold;

            AllocateSegment();
        }

        public ArraySegment<byte> GetRxSegment()
        {
            var freeSpace = GetFreeSpace(_currentSegment);

            if (freeSpace < _segmentSizeThreshold)
            {
                if (!_currentSegment.IsFullyConsumed)
                    _tail.Enqueue(_currentSegment);

                AllocateSegment();

                return new ArraySegment<byte>(_currentSegment.Bytes, 0, _segmentSize);
            }
            else
                return new ArraySegment<byte>(_currentSegment.Bytes, _currentSegment.Count, freeSpace);
        }

        public ArraySegment<byte> CommitDataRx(int dataSize)
        {
            var offset = _currentSegment.Count;

            _currentSegment.Count += dataSize;

            return new ArraySegment<byte>(_currentSegment.Bytes, offset, dataSize);
        }

        public void CommitDataConsume(int dataSize)
        {
            while (_tail.Count > 0)
            {
                var segment = _tail.Peek();

                var leftToConsume = segment.Count - segment.ConsumedCount;
                var toConsume = Math.Min(dataSize, leftToConsume);

                segment.ConsumedCount += toConsume;

                if (leftToConsume == toConsume) // fully consumed
                    DisposeSegment(_tail.Dequeue());

                dataSize -= toConsume;

                if (dataSize == 0)
                    return;
            }

            var leftToConsumeInCurrent = _currentSegment.Count - _currentSegment.ConsumedCount;

            if (leftToConsumeInCurrent == dataSize)
                _currentSegment.Reset();
            else if (leftToConsumeInCurrent > dataSize)
                _currentSegment.ConsumedCount += dataSize;
            else
                throw new Exception("There is no more data in buffer to consume!");
        }

        public void Dispose()
        {
            foreach (var segment in _tail)
                DisposeSegment(segment);

            _tail.Clear();

            DisposeSegment(_currentSegment);
            _currentSegment = default;
        }

        private void AllocateSegment()
        {
            _currentSegment = new RxSegment(ArrayPool<byte>.Shared.Rent(_segmentSize));
            //_currentSegment = new RxSegment(new byte[_segmentSize]);
        }

        private void DisposeSegment(RxSegment segment)
        {
            ArrayPool<byte>.Shared.Return(segment.Bytes);
        }

        private int GetFreeSpace(RxSegment segment)
        {
            return _segmentSize - segment.Count;
        }

        private struct RxSegment
        {
            public RxSegment(byte[] buffer)
            {
                Bytes = buffer;
                Count = 0;
                ConsumedCount = 0;
            }

            public byte[] Bytes { get; }
            public int Count { get; set; }
            public int ConsumedCount { get; set; }

            public bool IsFullyConsumed => Count == ConsumedCount;

            public void Reset()
            {
                Count = 0;
                ConsumedCount = 0;
            }
        }
    }
}
