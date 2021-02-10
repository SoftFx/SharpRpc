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

        public RxParseTask Advance(int size)
        {
            var rxTask = new RxParseTask();

            while (size > 0)
            {
                var segment = _segments.Dequeue();
                var fragmentSize = Math.Min(segment.Count, size);
                var fragment = new ArraySegment<byte>(segment.Array, 0, fragmentSize);
                size -= fragmentSize;

                rxTask.Add(fragment);

                //if (size >= _segments[0].Count)
                //{
                //    var seg = Shift();
                //    size -= seg.Count;
                //}
                //else
                //{
                //    _segments[0] = _segments[0].Slice(size);

                //    break;
                //}
            }

            ReplaceFilledSegments(rxTask.Count);

            return rxTask;
        }

        private void ReplaceFilledSegments(int count)
        {
            lock (_unusedSegmentCache)
            {
                for (int i = 0; i < count; i++)
                    _segments.Enqueue(GetNewSegment());
            }
        }

        //private ArraySegment<byte> Shift()
        //{
        //    var segOut = _segments.Dequeue();
        //    _segments.Enqueue(GetNewSegment());
        //    return segOut;
        //}

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

        //private class RxBufferSegment
        //{
        //    private readonly byte[] _array;
        //    private int _bytesFilled;
        //    private int _bytesConsumed;
        //    private bool _fillCompleted;
        //    private readonly RxBuffer _owner;

        //    public void AdvanceFill(int size, int minFreeSpace, out bool completed)
        //    {
        //        lock (_array)
        //        {
        //            _bytesFilled += size;
        //            if (_array.Length - _bytesFilled < minFreeSpace)
        //            {
        //                _fillCompleted = true;
        //                completed = true;
        //            }
        //            else
        //                completed = false;           
        //        }
        //    }

        //    public void AdvanceConsume(int size)
        //    {
        //        lock (_array)
        //        {
        //            _bytesConsumed += size;
        //            if (_fillCompleted && _bytesFilled == _bytesConsumed)
        //            {
        //                Reset();
        //                _owner.ReturnSegment(this);
        //            }
        //        }
        //    }

        //    private void Reset()
        //    {
        //        _bytesFilled = 0;
        //        _bytesConsumed = 0;
        //        _fillCompleted = false;
        //    }

        //    private class SegmentListAdapter : IList<ArraySegment<byte>>
        //    {
        //        private readonly IList<RxBufferSegment> _srcCollection;

        //        public ArraySegment<byte> this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        //        public int Count => throw new NotImplementedException();

        //        public bool IsReadOnly => throw new NotImplementedException();

        //        public void Add(ArraySegment<byte> item)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public void Clear()
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public bool Contains(ArraySegment<byte> item)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public void CopyTo(ArraySegment<byte>[] array, int arrayIndex)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public IEnumerator<ArraySegment<byte>> GetEnumerator()
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public int IndexOf(ArraySegment<byte> item)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public void Insert(int index, ArraySegment<byte> item)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public bool Remove(ArraySegment<byte> item)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        public void RemoveAt(int index)
        //        {
        //            throw new NotImplementedException();
        //        }

        //        IEnumerator IEnumerable.GetEnumerator()
        //        {
        //            throw new NotImplementedException();
        //        }
        //    }
        //}
    }

    public class RxParseTask : List<ArraySegment<byte>>
    {
        private int _bytesToConsume;

        public new void Add(ArraySegment<byte> segment)
        {
            _bytesToConsume += segment.Count;
            base.Add(segment);
        }

        public void AdvanceConsume(int size, out bool allConsumed)
        {
            allConsumed = Interlocked.Add(ref _bytesToConsume, -size) == 0;
        }
    }
}
