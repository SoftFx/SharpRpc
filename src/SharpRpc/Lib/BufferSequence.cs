using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Lib
{
    internal class BufferSequence<T>
    {
        private readonly List<Segment> _cachedSegments = new List<Segment>();
        private int _lastIndex;

        public ReadOnlySequence<T> GetSequence()
        {
            var first = _cachedSegments[0];
            var last = _cachedSegments[_lastIndex];

            return new ReadOnlySequence<T>(first, 0, last, last.Memory.Length);
        }

        public void Init(IReadOnlyList<ArraySegment<T>> segments)
        {
            ExpandCacheTo(segments.Count);

            var size = 0;

            for (int i = 0; i < segments.Count - 1; i++)
            {
                var arraySeg = segments[i];
                size += arraySeg.Count;
                _cachedSegments[i].Init(segments[0], _cachedSegments[i + 1], size);
            }

            _lastIndex = segments.Count - 1;
            var lastArrSeg = segments[_lastIndex];
            size += lastArrSeg.Count;
            _cachedSegments[_lastIndex].Init(lastArrSeg, null, size);
        }

        private void ExpandCacheTo(int newCount)
        {
            if (_cachedSegments.Count < newCount)
            {
                var toAdd = newCount - _cachedSegments.Count;

                for (int i = 0; i < toAdd; i++)
                    _cachedSegments.Add(new Segment());
            }
        }

        private class Segment : ReadOnlySequenceSegment<T>
        {
            public void Init(ArraySegment<T> buffer, Segment nextSegment, int size)
            {
                Memory = buffer;
                Next = nextSegment;
                RunningIndex = size;
            }
        }
    }
}
