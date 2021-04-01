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
