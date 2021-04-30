// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    partial class TxBuffer
    {
        private class MemoryManager
        {
            //private readonly Queue<byte[]> _cache = new Queue<byte[]>();
            //private readonly int _maxCacheSize;

            public MemoryManager(int segmentSize, int maxSegmentsToCache)
            {
                SegmentSize = segmentSize;
                //_maxCacheSize = maxSegmentsToCache;
            }

            public int SegmentSize { get; }

            public byte[] AllocateSegment()
            {
                return ArrayPool<byte>.Shared.Rent(SegmentSize);
                //if (_cache.Count > 0)
                //    return _cache.Dequeue();
                //else
                //    return new byte[SegmentSize];
            }

            public void FreeSegment(ArraySegment<byte> segment) => FreeSegment(segment.Array);

            public void FreeSegment(byte[] segBuffer)
            {
                //if (_cache.Count < _maxCacheSize)
                //    _cache.Enqueue(segBuffer);
                ArrayPool<byte>.Shared.Return(segBuffer, false);
            }
        }
    }
}
