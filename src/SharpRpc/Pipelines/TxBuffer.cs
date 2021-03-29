using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal partial class TxBuffer : IBufferWriter<byte>, MessageWriter
    {
        private readonly object _lockObj;
        private readonly StreamProxy _streamProxy;
        private readonly List<ArraySegment<byte>> _completeSegments = new List<ArraySegment<byte>>();
        private readonly MemoryManager _memManager;
        private readonly int _minAllocSize = 64;
        private readonly MessageMarker _marker;
        private DequeueRequest _dequeueWaitHandle;
        //private readonly Action _dataArrivedEvent;
        private readonly IRpcSerializer _serializer;

        public TxBuffer(object lockObj, int segmentSize, IRpcSerializer serializer)
        {
            _lockObj = lockObj;

            //if (segmentSize > ushort.MaxValue)
            //    throw new ArgumentException("Segment size must be less than " + ushort.MaxValue + ".");

            _serializer = serializer;

            //_dataArrivedEvent = dataArrivedCallback;

            _memManager = new MemoryManager(segmentSize, 5);
            //_minAllocSize = minSizeHint;
            _streamProxy = new StreamProxy(this);

            _marker = new MessageMarker(this);

            AllocNewSegment();
        }

        public bool IsCurrentSegmentLocked { get; private set; }
        public bool IsDataAvailable => IsCurrentDataAvailable || HasCompletedSegments;

        public int DataSize { get; private set; }

        private byte[] CurrentSegment { get; set; }
        private int CurrentOffset { get; set; }

        public event Action<TxBuffer> SpaceFreed;

        private bool IsCurrentDataAvailable => !IsCurrentSegmentLocked && CurrentOffset > 0;
        private bool HasCompletedSegments => _completeSegments.Count > 0;
        private int SegmentSize => _memManager.SegmentSize;

        public void Lock()
        {
            //lock (_lockObj)
            IsCurrentSegmentLocked = true;
        }

        //public DequeueRequest Unlock()
        //{
        //    IsCurrentSegmentLocked = false;
        //    return SignalDataAvailable();
        //}

        public void WriteMessage(MessageHeader header, IMessage message)
        {
            //lock (_lockObj)
            //    IsCurrentSegmentLocked = true;

            _marker.OnMessageStart(header);
            _serializer.Serialize(message, this);
            _marker.OnMessageEnd();

            DequeueRequest toSignal = null;

            lock (_lockObj)
            {
                IsCurrentSegmentLocked = false;
                toSignal = SignalDataAvailable();
            }

            toSignal?.Signal();
        }

        //public void ReleaseLock()
        //{
        //    //IsCurrentSegmentLocked = false;

        //    DequeueRequest toSignal = null;

        //    //lock (_lockObj)
        //    {
        //        IsCurrentSegmentLocked = false;
        //        toSignal = SignalDataAvailable();
        //    }

        //    toSignal?.Signal();
        //}

        //public void StartMessageWrite(MessageHeader header)
        //{
        //    _marker.OnMessageStart(header);
        //}

        //public void EndMessageWrite()
        //{
        //    _marker.OnMessageEnd();
        //}

        public ValueTask ReturnAndDequeue(List<ArraySegment<byte>> container)
        {
            //if (IsCurrentDataAvailable)
            //    CompleteCurrentSegment();

            //toContainer.AddRange(_completeSegments);
            //_completeSegments.Clear();

            lock (_lockObj)
            {
                foreach (var segment in container)
                    _memManager.FreeSegment(segment);

                container.Clear();

                var hasCurrentData = IsCurrentDataAvailable;

                if (HasCompletedSegments || hasCurrentData)
                {
                    DequeueTo(container, hasCurrentData);
                    SpaceFreed?.Invoke(this);
                    return new ValueTask();
                }
                else
                {
                    _dequeueWaitHandle = new DequeueRequest(container);
                    return new ValueTask(_dequeueWaitHandle.Task);
                }
            }
        }

        public void ReturnSegments(List<ArraySegment<byte>> container)
        {
            foreach (var segment in container)
                _memManager.FreeSegment(segment);
        }

        private DequeueRequest SignalDataAvailable()
        {
            var cpy = _dequeueWaitHandle;
            _dequeueWaitHandle = null;

            if (cpy != null)
                DequeueTo(cpy.Container, IsCurrentDataAvailable);

            return cpy;
        }

        private void DequeueTo(List<ArraySegment<byte>> container, bool hasCurrentData)
        {
            if (hasCurrentData)
                CompleteCurrentSegment();

            foreach (var segment in _completeSegments)
            {
                DataSize -= segment.Count;
                container.Add(segment);
            }

            _completeSegments.Clear();
        }

        #region IBufferWriter implementation

        public void Advance(int count)
        {
            MoveOffset(count);
            //_currentOffset += count;
            //Size += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureSpace(sizeHint);
            return new Memory<byte>(CurrentSegment, CurrentOffset, SegmentSize - CurrentOffset);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            return new Span<byte>(CurrentSegment, CurrentOffset, SegmentSize - CurrentOffset);
        }

        #endregion

        private void EnsureSpace(int sizeHint)
        {
            if (sizeHint <= _minAllocSize)
                sizeHint = _minAllocSize;

            var spaceInCurrentSegment = SegmentSize - CurrentOffset;
            //var spaceInCurrentChunk = _marker.GetCurrentChunkCapacity();

            if (spaceInCurrentSegment < sizeHint)
                CompleteCurrentSegment();

            _marker.OnAlloc();

            //else if (spaceInCurrentChunk < sizeHint)
            //ReopenChunk();
        }

        private void CompleteCurrentSegment()
        {
            _marker.OnSegmentClose();

            DequeueRequest toSignal = null;

            lock (_lockObj)
            {
                _completeSegments.Add(new ArraySegment<byte>(CurrentSegment, 0, CurrentOffset));

                AllocNewSegment();

                toSignal = SignalDataAvailable();
            }

            toSignal?.Signal();
        }

        private void AllocNewSegment()
        {
            CurrentSegment = _memManager.AllocateSegment();
            CurrentOffset = 0;
        }

        private void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();

            //while (count > 0)
            //{
            //    var space = _segmentSize - _currentOffset;
            //    var copyOpSize = Math.Min(count, space);

            //    //Array.Copy(buffer, offset, _currentSegment.Memory.Span, _currentOffset, toCopy);

            //    var srcSpan = buffer.AsSpan(offset, copyOpSize);
            //    var dstSpan = srcSpan.Slice(_currentOffset, copyOpSize);

            //    srcSpan.CopyTo(dstSpan);

            //    count -= copyOpSize;
            //    _currentOffset += copyOpSize;

            //    if (_currentOffset >= _segmentSize)
            //    {
            //        Comple

            //        _completeSegments.Add(new ArraySegment<byte>(_currentSegment, 0, _currentOffset));
            //        _currentSegment = new byte[_segmentSize];
            //        _currentOffset = 0;
            //    }
            //}
        }

        private void MoveOffset(int size)
        {
            CurrentOffset += size;
            DataSize += size;

            //if (CurrentOffset > SegmentSize)
            //{
            //}
        }

        #region MessageWriter implementation

        IBufferWriter<byte> MessageWriter.ByteBuffer => this;
        System.IO.Stream MessageWriter.ByteStream => _streamProxy;

        #endregion

        public class DequeueRequest : TaskCompletionSource<bool>
        {
            public DequeueRequest(List<ArraySegment<byte>> container)
            {
                Container = container;
            }

            public List<ArraySegment<byte>> Container { get; }

            public void Signal()
            {
                SetResult(true);
            }
        }
    }

    //internal struct ByteSegment
    //{
    //    public ByteSegment(byte[] bytes, int len)
    //    {
    //        Bytes = bytes;
    //        Length = len;
    //    }

    //    public byte[] Bytes { get; }
    //    public int Length { get; }

    //    public ArraySegment<byte> ToArraySegment()
    //    {
    //        return new ArraySegment<byte>(Bytes, 0, Length);
    //    }
    //}
}
