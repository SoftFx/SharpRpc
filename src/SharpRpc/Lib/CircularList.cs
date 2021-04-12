using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Lib
{
    public class CircularList<T> : IReadOnlyList<T>, IList<T>
    {
        private static readonly T[] emptyBuffer = new T[0];

        private int _begin;
        private int _end;
        private T[] _buffer;

        public CircularList()
        {
            _buffer = emptyBuffer;
            ResetPointers();
        }

        public CircularList(int capacity)
        {
            _buffer = new T[capacity];
            ResetPointers();
        }

        public int Capacity { get { return _buffer.Length; } }

        public void Enqueue(T item)
        {
            Add(item);
        }

        public virtual void Add(T item)
        {
            if (Count == Capacity)
                Expand();

            if (++_end >= Capacity)
                _end = 0;

            _buffer[_end] = item;
            Count++;
        }

        public void AddRange(IEnumerable<T> recRange)
        {
            // TO DO : optimization in case recRange is IList or ICollection

            foreach (T rec in recRange)
                Add(rec);
        }

        public virtual T Dequeue()
        {
            if (Count == 0)
                throw new InvalidOperationException("List is empty!");

            T result = _buffer[_begin];
            _buffer[_begin] = default(T);

            Count--;

            if (++_begin == Capacity)
                _begin = 0;

            return result;
        }

        public int DequeueRange(Span<T> container)
        {
            var dSize = Math.Min(container.Length, Count);
            Span<T> srcBuf = _buffer;

            if (_begin <= _end)
            {
                srcBuf.Slice(_begin, dSize).CopyTo(container);
                Array.Clear(_buffer, _begin, dSize);

                _begin += dSize;
            }
            else
            {
                var firstPartLen = Capacity - _begin;
                //var d1Size = Math.Min(firstPartLen, dSize);

                if (dSize < firstPartLen)
                {
                    srcBuf.Slice(_begin, dSize).CopyTo(container);
                    Array.Clear(_buffer, _begin, dSize);

                    _begin += dSize;
                }
                else if (dSize == firstPartLen)
                {
                    srcBuf.Slice(_begin, dSize).CopyTo(container);
                    Array.Clear(_buffer, _begin, dSize);

                    _begin = 0;
                }
                else
                {
                    srcBuf.Slice(_begin, firstPartLen).CopyTo(container);
                    Array.Clear(_buffer, _begin, firstPartLen);

                    _begin = dSize - firstPartLen;

                    srcBuf.Slice(0, _begin).CopyTo(container.Slice(firstPartLen));
                    Array.Clear(_buffer, 0, _begin);
                }
            }

            Count -= dSize;

            if (Count == 0)
                ResetPointers();

            return dSize;
        }

        public List<T> DequeueRange(int maxItems = int.MaxValue)
        {
            var list = new List<T>();
            DequeueRange(list, maxItems);
            return list;
        }

        public int DequeueRange(List<T> container, int maxItems)
        {
            var dSize = Math.Min(maxItems, Count);

            if (_begin <= _end)
            {
                container.AddRange(new ArraySegment<T>(_buffer, _begin, dSize));
                Array.Clear(_buffer, _begin, dSize);

                _begin += dSize;
            }
            else
            {
                var firstPartLen = Capacity - _begin;

                if (dSize < firstPartLen)
                {
                    container.AddRange(new ArraySegment<T>(_buffer, _begin, dSize));
                    Array.Clear(_buffer, _begin, dSize);

                    _begin += dSize;
                }
                else if (dSize == firstPartLen)
                {
                    container.AddRange(new ArraySegment<T>(_buffer, _begin, dSize));
                    Array.Clear(_buffer, _begin, dSize);

                    _begin = 0;
                }
                else
                {
                    container.AddRange(new ArraySegment<T>(_buffer, _begin, firstPartLen));
                    Array.Clear(_buffer, _begin, firstPartLen);

                    _begin = dSize - firstPartLen;

                    container.AddRange(new ArraySegment<T>(_buffer, 0, _begin));
                    Array.Clear(_buffer, 0, _begin);
                }
            }

            Count -= dSize;

            if (Count == 0)
                ResetPointers();

            return dSize;
        }

        public virtual void Clear()
        {
            if (Count == 0)
                return;

            if (_begin <= _end)
                Array.Clear(_buffer, _begin, _begin - _end + 1);
            else
            {
                Array.Clear(_buffer, _begin, Capacity - _begin);
                Array.Clear(_buffer, 0, _end + 1);
            }

            Count = 0;
            ResetPointers();
        }

        //public virtual void TruncateStart(int tSize)
        //{
        //    if (tSize == 0)
        //        return;

        //    if (tSize < 0 || tSize > Count)
        //        throw new ArgumentOutOfRangeException();

        //    DoTruncateStart(tSize);
        //}

        //protected virtual void DoTruncateStart(int tSize)
        //{
        //    if (_begin <= _end)
        //    {
        //        Array.Clear(_buffer, _begin, tSize);
        //        _begin += tSize;
        //    }
        //    else
        //    {
        //        var firstPartLen = Capacity - _begin;
        //        if (tSize < firstPartLen)
        //        {
        //            Array.Clear(_buffer, _begin, tSize);
        //            _begin += tSize;
        //        }
        //        else if (tSize == firstPartLen)
        //        {
        //            Array.Clear(_buffer, _begin, tSize);
        //            _begin = 0;
        //        }
        //        else
        //        {
        //            Array.Clear(_buffer, _begin, firstPartLen);
        //            _begin = tSize - firstPartLen;
        //            Array.Clear(_buffer, 0, _begin);
        //        }
        //    }

        //    Count -= tSize;

        //    if (Count == 0)
        //    {
        //        _begin = 0;
        //        _end = -1;
        //    }
        //}

        private void Expand()
        {
            int expandBy = Capacity > 0 ? Capacity : 4;

            var oldBuffer = _buffer;
            _buffer = new T[Capacity + expandBy];

            if (Count != 0)
            {
                if (_begin <= _end)
                    Array.Copy(oldBuffer, _begin, _buffer, 0, Count);
                else
                {
                    var firstPartLength = oldBuffer.Length - _begin;
                    // copy first part
                    Array.Copy(oldBuffer, _begin, _buffer, 0, firstPartLength);
                    // copy second part
                    Array.Copy(oldBuffer, 0, _buffer, firstPartLength, _end + 1);
                }
            }

            _begin = 0;
            _end = Count - 1;
        }

        private int CalculateBufferIndex(int queueIndex)
        {
            if (queueIndex < 0 || queueIndex >= Count)
                throw new ArgumentOutOfRangeException();

            int realIndex = _begin + queueIndex;
            if (realIndex >= _buffer.Length)
                realIndex -= _buffer.Length;
            return realIndex;
        }

        public T this[int index]
        {
            get { return _buffer[CalculateBufferIndex(index)]; }
            set { _buffer[CalculateBufferIndex(index)] = value; }
        }

        public int Count { get; private set; }

        public bool IsReadOnly => false;

        public IEnumerator<T> GetEnumerator()
        {
            if (Count != 0)
            {
                if (_begin <= _end)
                {
                    for (int i = _begin; i <= _end; i++)
                        yield return _buffer[i];
                }
                else
                {
                    for (int i = _begin; i < _buffer.Length; i++)
                        yield return _buffer[i];

                    for (int i = 0; i <= _end; i++)
                        yield return _buffer[i];
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int IndexOf(T item)
        {
            int index = 0;

            foreach (var r in this)
            {
                if (item.Equals(r))
                    return index;

                index++;
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
            //Array.Copy(this.buffer, 0, array, arrayIndex, this.Count);
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        private void ResetPointers()
        {
            _begin = 0;
            _end = -1;
        }
    }
}
