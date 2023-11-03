// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Diagnostics;

namespace SharpRpc
{
    internal class RxBuffer
    {
        private const int MaxThreshold = 1024 * 5;

        private readonly CircularList<RxSegment> _tail = new CircularList<RxSegment>();
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

            _currentSegment = _currentSegment.IncreaseCount(dataSize);

            return new ArraySegment<byte>(_currentSegment.Bytes, offset, dataSize);
        }

        public void CommitDataConsume(long dataSize)
        {
            while (_tail.Count > 0)
            {
                var segment = _tail[0];

                var leftToConsume = segment.Count - segment.ConsumedCount;
                var toConsume = (int)Math.Min(dataSize, leftToConsume);

                segment = segment.IncreaseConsumedCount(toConsume);

                if (leftToConsume == toConsume) // fully consumed
                    DisposeSegment(_tail.Dequeue());
                else
                    _tail[0] = segment;

                dataSize -= toConsume;

                if (dataSize == 0)
                    return;
            }

            var leftToConsumeInCurrent = _currentSegment.Count - _currentSegment.ConsumedCount;

            if (leftToConsumeInCurrent >= dataSize)
            {
                _currentSegment = _currentSegment.IncreaseConsumedCount((int)dataSize);
                if (_currentSegment.IsFullyConsumed)
                    _currentSegment = _currentSegment.Reset();
            }
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
            _currentSegment = new RxSegment(System.Buffers.ArrayPool<byte>.Shared.Rent(_segmentSize));
        }

        private void DisposeSegment(RxSegment segment)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(segment.Bytes);
        }

        private int GetFreeSpace(RxSegment segment)
        {
            return _segmentSize - segment.Count;
        }

        private readonly struct RxSegment
        {
            public RxSegment(byte[] buffer, int count, int consumedCount)
            {
                Bytes = buffer;
                Count = count;
                ConsumedCount = consumedCount;
            }

            public RxSegment(byte[] buffer)
            {
                Bytes = buffer;
                Count = 0;
                ConsumedCount = 0;
            }

            public byte[] Bytes { get; }
            public int Count { get; }
            public int ConsumedCount { get; }

            public bool IsFullyConsumed => Count <= ConsumedCount;

            public RxSegment IncreaseCount(int value)
            {
                return new RxSegment(Bytes, Count + value, ConsumedCount);
            }

            public RxSegment IncreaseConsumedCount(int value)
            {
                return new RxSegment(Bytes, Count, ConsumedCount + value);
            }

            public RxSegment Reset()
            {
                return new RxSegment(Bytes);
            }
        }
    }
}
