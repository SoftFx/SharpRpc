using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    internal class BatchingActionBlock<T> : IActionBlock<T>
    {
        private readonly object _lockObj = new object();
        private Task _worker;
        private readonly CircularList<T> _queue = new CircularList<T>();
        private readonly List<T> _batch = new List<T>();
        private readonly TaskCompletionSource<object> _completion = new TaskCompletionSource<object>();
        private bool _completed;
        private readonly Action<IList<T>> _batchAction;
        private readonly int _maxBatchSize;
        private readonly int _maxQueueSize;
        private readonly Queue<EnqueuePendingTask> _waiters = new Queue<EnqueuePendingTask>();

        //private static Task<bool> FalseTask = Task.FromResult(false);
        //private static Task<bool> TrueTask = Task.FromResult(true);

        public BatchingActionBlock(Action<IList<T>> batchAction, int maxBatchSize, int maxQueueSize = -1)
        {
            if (maxBatchSize <= 0)
                throw new ArgumentException("maxBatchSize must be positive integer.");

            _batchAction = batchAction;
            _maxBatchSize = maxBatchSize;
            _maxQueueSize = maxQueueSize;
        }

        public int QueueSize => _queue.Count;

        public ValueTask<bool> TryEnqueueAsync(T item)
        {
            lock (_lockObj)
            {
                if (_completed)
                    return new ValueTask<bool>(false);

                if (_maxQueueSize > 0 && _queue.Count >= _maxQueueSize)
                {
                    var waiter = new EnqueuePendingTask(item);
                    _waiters.Enqueue(waiter);
                    return new ValueTask<bool>(waiter.Task);
                }

                _queue.Enqueue(item);
                ScheduleNextTask();
                return new ValueTask<bool>(true);
            }
        }

        public bool TryEnqueue(T item)
        {
            lock (_lockObj)
            {
                if (_maxQueueSize > 0)
                {
                    while (_queue.Count >= _maxQueueSize)
                        Monitor.Wait(_lockObj);
                }

                if (_completed)
                    return false;

                _queue.Enqueue(item);
                ScheduleNextTask();
                return true;
            }
        }

        private void ScheduleNextTask()
        {
            if (_worker != null)
                return;

            _batch.Clear();

            if (_queue.Count > 0)
            {
                _queue.DequeueRange(_batch, _maxBatchSize);

                _worker = Task.Factory.StartNew(ProcessItems);
                if (_maxQueueSize > 0)
                    Monitor.PulseAll(_lockObj);
                ReleaseWaiters();
            }
            else if (_completed)
            {
                CancelWaiters();
                _completion.TrySetResult(this);
            }
        }

        private void ReleaseWaiters()
        {
            if (_maxQueueSize <= 0)
                return;

            while (_queue.Count < _maxQueueSize && _waiters.Count > 0)
            {
                var waiter = _waiters.Dequeue();
                _queue.Enqueue(waiter.ItemToEnqueue);
                waiter.SetResult(true);
            }
        }

        private void CancelWaiters()
        {
            while (_queue.Count > 0)
            {
                var waiter = _waiters.Dequeue();
                waiter.SetResult(false);
            }
        }

        private void ProcessItems()
        {
            _batchAction(_batch);

            lock (_lockObj)
            {
                _worker = null;
                ScheduleNextTask();
            }
        }

        public void Close(int msTimeout)
        {
            var closeTask = Close();

            if (!closeTask.Wait(msTimeout))
                throw new TimeoutException("Failed to gracefully stop an inner worker thread within the specified timeout. The thread is aborted.");
        }

        public Task Close()
        {
            lock (_lockObj)
            {
                _completed = true;
                if (_queue.Count == 0)
                    _completion.TrySetResult(this);
            }

            return _completion.Task;
        }

        private class EnqueuePendingTask : TaskCompletionSource<bool>
        {
            public EnqueuePendingTask(T item)
            {
                ItemToEnqueue = item;
            }

            public T ItemToEnqueue { get; }
        }
    }
}
