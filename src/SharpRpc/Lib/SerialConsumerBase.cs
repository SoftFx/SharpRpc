// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    internal abstract class SerialConsumerBase<T>
    {
        private bool _isProcessing;
        private List<T> _batch = new List<T>();
        private List<T> _queue = new List<T>();
        private Task _workerTask;
        private bool _completed;
        private readonly TaskCompletionSource<bool> _closedSrc = new TaskCompletionSource<bool>();

        protected IReadOnlyList<T> Batch => _batch;
        protected int QueueSize => _queue.Count;
        protected bool IsProcessing => _isProcessing;
        protected bool IsCompleted => _completed;
        protected abstract bool CanEnqueueMore { get; }

#if NET5_0_OR_GREATER
        protected ValueTask Enqueue(T item)
#else
        protected Task Enqueue(T item)
#endif
        {
            if (_completed)
                throw new InvalidOperationException();

            _queue.Add(item);

            if (!IsProcessing)
                EnqueueNextBatch();

            if (!CanEnqueueMore)
                return FwAdapter.WrappResult(_workerTask);

            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        protected ValueTask Enqueue(ArraySegment<T> items)
#else
        protected Task Enqueue(ArraySegment<T> items)
#endif
        {
            if (_completed)
                throw new InvalidOperationException();

            _queue.AddRange(items);

            if (!IsProcessing)
                EnqueueNextBatch();

            if (!CanEnqueueMore)
                return FwAdapter.WrappResult(_workerTask);

            return FwAdapter.AsyncVoid;
        }

        protected Task Close(bool clearQueue)
        {
            _completed = true;

            if (clearQueue)
                _queue.Clear();

            if (_isProcessing)
                return _closedSrc.Task;

            return Task.CompletedTask;
        }

        private void EnqueueNextBatch()
        {
            _isProcessing = true;

            var cpy = _queue;
            _queue = _batch;
            _batch = cpy;

            _workerTask = ProcessBatch();
        }

        protected void OnBatchCompleted()
        {
            _isProcessing = false;
            _batch.Clear();

            if (_queue.Count > 0)
                EnqueueNextBatch();
            else if (_completed)
                _closedSrc.SetResult(true);
        }

        protected abstract Task ProcessBatch();
    }
}
