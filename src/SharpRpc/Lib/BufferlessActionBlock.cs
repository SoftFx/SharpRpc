using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    //public class BufferlessActionBlock<T> : IActionBlock<T>
    //{
    //    private readonly object _lockObj = new object();
    //    private readonly Action<T> _action;
    //    private bool _isProcessingItem;
    //    private bool _isCompleted;
    //    private readonly Queue<PendingItemTask> _asyncQueue = new Queue<PendingItemTask>();
    //    private readonly TaskCompletionSource<object> _completedEvent = new TaskCompletionSource<object>();

    //    public BufferlessActionBlock(Action<T> action)
    //    {
    //        _action = action;
    //    }

    //    public bool TryEnqueue(T item)
    //    {
    //        lock (_lockObj)
    //        {
    //            if (_isCompleted)
    //                return false;

    //            while (_isProcessingItem)
    //            {
    //                Monitor.Wait(_lockObj);

    //                if (_isCompleted)
    //                    return false;
    //            }

    //            _isProcessingItem = true;
    //        }

    //        ProcessItem(item);

    //        return true;
    //    }

    //    public ValueTask<bool> TryEnqueueAsync(T item)
    //    {
    //        lock (_lockObj)
    //        {
    //            if (_isCompleted)
    //                return new ValueTask<bool>(false);

    //            if (_isProcessingItem)
    //            {
    //                var waitItem = new PendingItemTask(item);
    //                _asyncQueue.Enqueue(waitItem);
    //                return new ValueTask<bool>(waitItem.Task);
    //            }
    //            else
    //                _isProcessingItem = true;
    //        }

    //        ProcessItem(item);

    //        return new ValueTask<bool>(true);
    //    }

    //    private void ProcessItem(T item)
    //    {
    //        try
    //        {
    //            _action(item);
    //        }
    //        finally
    //        {
    //            lock (_lockObj)
    //            {
    //                _isProcessingItem = false;
    //                ProceedToNextItem();
    //            }
    //        }
    //    }

    //    private void ProceedToNextItem()
    //    {
    //        if (_asyncQueue.Count > 0)
    //        {
    //            var nextAsyncItem = _asyncQueue.Dequeue();
    //            Task.Factory.StartNew(()=> 
    //        }
    //        else
    //            Monitor.Pulse(_lockObj);
    //    }

    //    public Task CloseAsync()
    //    {
    //        lock (_lockObj)
    //        {
    //            if (!_isCompleted)
    //            {
    //                _isCompleted = true;

    //                Monitor.PulseAll(_lockObj);

    //                while (_asyncQueue.Count > 0)
    //                    _asyncQueue.Dequeue().SetCanceled();

    //                if (!_isProcessingItem)
    //                    _completedEvent.TrySetResult(true);
    //            }
    //        }

    //        return _completedEvent.Task;
    //    }

    //    private class PendingItemTask : TaskCompletionSource<bool>
    //    {
    //        public PendingItemTask(T item)
    //        {
    //            ItemToEnqueue = item;
    //        }

    //        public T ItemToEnqueue { get; }
    //    }
    //}
}
