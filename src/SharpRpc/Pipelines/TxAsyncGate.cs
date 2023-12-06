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
            Item waitItem = throwing ? (Item)new AsyncThrowItem(message) :  new AsyncTryItem(message);

            if (system)
                _systemQueue.Enqueue(waitItem);
            else
                _userQueue.Enqueue(waitItem);

            return waitItem.Task;
        }

        public void Enqueue(IMessage message, bool system, Action<RpcResult> onSendCompletedCallback)
        {
            if (system)
                _systemQueue.Enqueue(new CallbackItem(message, onSendCompletedCallback));
            else
                _userQueue.Enqueue(new CallbackItem(message, onSendCompletedCallback));
        }

        public bool TryCancelUserMessage(IMessage message)
        {
            foreach (var item in _userQueue)
            {
                if (item.Message == message)
                {
                    item.Canceled = true;
                    return true;
                }
            }

            return false;
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

            if (userMessagesEnabled)
            {
                while (_userQueue.Count > 0)
                {
                    var item = _userQueue.Dequeue();

                    if (!item.Canceled)
                        return item;
                }
            }

            return null;
        }

        public interface Item
        {
            IMessage Message { get; }
            bool Canceled { get; set; }
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
            public bool Canceled { get; set; }

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
            public AsyncTryItem(IMessage msg)
            {
                Message = msg;
            }

            public IMessage Message { get; }
            public bool Canceled { get; set; }

            public void OnResult(RpcResult result)
            {
                SetResult(result);
            }
        }

        private class CallbackItem : Item
        {
            private readonly Action<RpcResult> _callback;

            public CallbackItem(IMessage msg, Action<RpcResult> callback)
            {
                Message = msg;
                _callback = callback;
            }

            public IMessage Message { get; }
            public Task<RpcResult> Task => null;
            public bool Canceled { get; set; }

            public void OnResult(RpcResult result)
            {
                _callback(result);
            }
        }
    }
}
