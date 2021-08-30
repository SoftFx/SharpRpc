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
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class MessageDispatcher
    {
        private class NoThreading : MessageDispatcher
        {
            private readonly object _lockObj = new object();
            private bool _isProcessing;
            private bool _closed;
            //private RpcResult _fault;
            private TaskCompletionSource<bool> _closeCompletion = new TaskCompletionSource<bool>();

            public override void Start()
            {
            }

            public override Task Stop(RpcResult fault)
            {
                lock (_lockObj)
                {
                    _closed = true;
                    //_fault = fault;

                    //tasksToCanel = _callTasks.Values.ToList();
                    //_callTasks.Clear();

                    Core.OnStop(fault);

                    if (!_isProcessing)
                        CompleteClose();
                }

                Core.CompleteStop();

                return _closeCompletion.Task;
            }

            public override RpcResult OnSessionEstablished()
            {
                return Core.FireOpened();

                //try
                //{
                //    ((RpcCallHandler)MessageHandler).Session.FireOpened(new SessionOpenedEventArgs());
                //}
                //catch (Exception)
                //{
                //    TO DO : log or pass some more information about expcetion(stack trace)
                //    return new RpcResult(RpcRetCode.RequestCrashed, "An exception has been occured in ");
                //}

                //lock (_lockObj)
                //    _allowFlag = true;

                //return RpcResult.Ok;
            }

#if NET5_0_OR_GREATER
            public override ValueTask OnMessages()
#else
            public override Task OnMessages()
#endif
            {
                lock (_lockObj)
                {
                    if (_closed)
                        return FwAdapter.AsyncVoid;

                    _isProcessing = true;
                }

                foreach (var msg in IncomingMessages)
                    Core.ProcessMessage(msg);

                lock (_lockObj)
                {
                    _isProcessing = false;

                    if (_closed)
                        CompleteClose();
                }

                return FwAdapter.AsyncVoid;
            }

            protected override async void DoCall(IRequest requestMsg, MessageDispatcherCore.IInteropOperation callTask)
            {
                var callId = GenerateOperationId(); // Guid.NewGuid().ToString();

                requestMsg.CallId = callId;

                var result = Core.TryRegisterOperation(callId, callTask);

                if (result.Code == RpcRetCode.Ok)
                {
                    result = await Tx.TrySendAsync(requestMsg);

                    if (result.Code != RpcRetCode.Ok)
                    {
                        if (!Core.UnregisterOperation(callId))
                            return; // do not need to call Task.Fail() in this case, because it was called in Close() method
                    }
                }

                if (result.Code != RpcRetCode.Ok)
                    callTask.Fail(result);
            }

            public override RpcResult RegisterCallObject(string callId, MessageDispatcherCore.IInteropOperation callTask)
            {
                return Core.TryRegisterOperation(callId, callTask);
            }

            public override void UnregisterCallObject(string callId)
            {
                Core.UnregisterOperation(callId);
            }

            private void InvokeOnStop()
            {
                Core.FireClosed();

                _closeCompletion.SetResult(true);
            }

            private void CompleteClose()
            {
                TaskQueue.StartNew(InvokeOnStop);
            }
        }
    }
}
