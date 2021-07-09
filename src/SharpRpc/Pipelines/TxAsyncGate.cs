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

namespace SharpRpc
{
    internal class TxAsyncGate
    {
        private readonly Queue<Item> _userQueue = new Queue<Item>();
        private readonly Queue<Item> _systemQueue = new Queue<Item>();

        public int UserQueueSize => _userQueue.Count;

        public Task<RpcResult> Enqueue(IMessage message, bool throwing, bool system)
        {
            Item waitItem = throwing ? (Item)new AsyncThrowItem(message) :  (Item)new AsyncTryItem(message);

            if (system)
                _systemQueue.Enqueue(waitItem);
            else
                _userQueue.Enqueue(waitItem);

            return waitItem.Task;
        }

        public void CancelUserItems(RpcResult fault)
        {
            while (_userQueue.Count > 0)
                _userQueue.Dequeue().OnResult(fault);
        }

        public void CancelSysytemItems(RpcResult fault)
        {
            while (_systemQueue.Count > 0)
                _systemQueue.Dequeue().OnResult(fault);
        }

        public Item Dequeue(bool userMessagesEnabled)
        {
            if (_systemQueue.Count > 0)
                return _systemQueue.Dequeue();
            else if (userMessagesEnabled && _userQueue.Count > 0)
                return _userQueue.Dequeue();
            else
                return null;
        }

        public interface Item
        {
            IMessage Message { get; }
            void OnResult(RpcResult result);
            Task<RpcResult> Task { get; }
        }

        private class AsyncThrowItem : TaskCompletionSource<RpcResult>, Item
        {
            public AsyncThrowItem(IMessage item)
            {
                Message = item;
            }

            public IMessage Message { get; }

            public void OnResult(RpcResult result)
            {
                if (result.Code == RpcRetCode.Ok)
                    SetResult(result);
                else
                    TrySetException(result.ToException());
            }
        }

        private class AsyncTryItem : TaskCompletionSource<RpcResult>, Item
        {
            public AsyncTryItem(IMessage item)
            {
                Message = item;
            }

            public IMessage Message { get; }

            public void OnResult(RpcResult result)
            {
                SetResult(result);
            }
        }
    }
}
