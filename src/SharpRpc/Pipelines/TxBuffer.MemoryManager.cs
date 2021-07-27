// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    partial class TxBuffer
    {
        private abstract class MemoryManager
        {
            public static MemoryManager Create(int segmentSize, int maxSegmentsToCache)
            {
#if NET5_0_OR_GREATER
                return new PoolBasedManager(segmentSize);
#else
                return new CacheBasedManager(segmentSize, maxSegmentsToCache);
#endif
            }

            public int SegmentSize { get; protected set; }

            public abstract byte[] AllocateSegment();
            public abstract void FreeSegment(byte[] segBuffer);

            public void FreeSegment(ArraySegment<byte> segment) => FreeSegment(segment.Array);
        }

#if NET5_0_OR_GREATER
        private class PoolBasedManager : MemoryManager
        {
            public PoolBasedManager(int segmentSize)
            {
                SegmentSize = segmentSize;
            }

            public override byte[] AllocateSegment()
            {
                return System.Buffers.ArrayPool<byte>.Shared.Rent(SegmentSize);
            }

            public override void FreeSegment(byte[] segBuffer)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(segBuffer, false);
            }
        }
#endif

        private class CacheBasedManager : MemoryManager
        {
            private readonly Queue<byte[]> _cache = new Queue<byte[]>();
            private readonly int _maxCacheSize;

            public CacheBasedManager(int segmentSize, int maxSegmentsToCache)
            {
                SegmentSize = segmentSize;
                _maxCacheSize = maxSegmentsToCache;
            }

            public override byte[] AllocateSegment()
            {
                if (_cache.Count > 0)
                    return _cache.Dequeue();
                else
                    return new byte[SegmentSize];
            }

            public override void FreeSegment(byte[] segBuffer)
            {
                if (_cache.Count < _maxCacheSize)
                    _cache.Enqueue(segBuffer);
            }
        }
    }
}
