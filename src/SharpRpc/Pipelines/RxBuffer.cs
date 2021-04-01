using SharpRpc.Lib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SharpRpc
{
    internal class RxBuffer
    {
        private readonly CircularList<ArraySegment<byte>> _segments = new CircularList<ArraySegment<byte>>();
        private readonly Queue<byte[]> _unusedSegmentCache = new Queue<byte[]>();
        private readonly int _segmentSize = 64 * 1024;
        private readonly int _segmentCount = 5;
        private readonly int _segmentCacheSize = 5;

        public RxBuffer()
        {
            for (int i = 0; i < _segmentCount; i++)
                _segments.Enqueue(new byte[_segmentSize]);
        }

        public IList<ArraySegment<byte>> Segments => _segments;

        public int Advance(int size, IList<ArraySegment<byte>> container)
        {
            var bytesCount = 0;

            while (size > 0)
            {
                var segment = _segments.Dequeue();
                var fragmentSize = Math.Min(segment.Count, size);
                var fragment = new ArraySegment<byte>(segment.Array, 0, fragmentSize);
                size -= fragmentSize;

                container.Add(fragment);

                bytesCount += fragmentSize;
            }

            ReplaceFilledSegments(container.Count);

            return bytesCount;
        }

        private void ReplaceFilledSegments(int count)
        {
            lock (_unusedSegmentCache)
            {
                for (int i = 0; i < count; i++)
                    _segments.Enqueue(GetNewSegment());
            }
        }

        private ArraySegment<byte> GetNewSegment()
        {
            if (_unusedSegmentCache.Count > 0)
                return _unusedSegmentCache.Dequeue();
            else
                return new byte[_segmentSize];
        }

        public void ReturnSegments(IList<ArraySegment<byte>> segments)
        {
            lock (_unusedSegmentCache)
            {
                foreach (var seg in segments)
                {
                    if (_unusedSegmentCache.Count < _segmentCacheSize)
                        _unusedSegmentCache.Enqueue(seg.Array);
                }
            }
        }
    }
}
