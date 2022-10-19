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
    internal class MessageDispatcherCore
    {
        private readonly Dictionary<string, IInteropOperation> _operations = new Dictionary<string, IInteropOperation>();
        private bool _completed;
        private RpcResult _fault;
        private List<IInteropOperation> _tasksToCancel;

        public MessageDispatcherCore(TxPipeline txNode, RpcCallHandler msgHandler, Action<RpcRetCode, string> onErrorAction)
        {
            Tx = txNode;
            MessageHandler = msgHandler;
            OnError = onErrorAction;
        }

        public TxPipeline Tx { get; }
        public RpcCallHandler MessageHandler { get; }
        public Action<RpcRetCode, string> OnError { get; }
        
        public RpcResult TryRegisterOperation(string callId, IInteropOperation callTask)
        {
            lock (_operations)
            {
                if (_completed)
                    return _fault;

                _operations.Add(callId, callTask);

                return RpcResult.Ok;
            }
        }

        public bool UnregisterOperation(string callId)
        {
            lock (_operations)
                return _operations.Remove(callId);
        }

        public void ProcessMessage(IMessage message)
        {
            if (message is IInteropMessage iMsg)
            {
                if (message is IResponseMessage resp)
                    ProcessResponse(resp);
                else if (message is IRequestMessage req)
                    ProcessRequest(req);
                else// if (message is IStreamAuxMessage auxMsg)
                    ProcessAxuMessage(iMsg);
            }
            else
                ProcessOneWayMessage(message);
        }

        public void OnStop(RpcResult fault)
        {
            lock (_operations)
            {
                _completed = true;
                _fault = fault;

                _tasksToCancel = _operations.Values.ToList();
                _operations.Clear();
            }
        }

        public void CompleteStop()
        {
            foreach (var task in _tasksToCancel)
                task.OnFail(_fault);
        }

        public bool TryInvokeInit(Channel ch, out RpcResult error)
        {
            try
            {
                MessageHandler.InvokeInit(ch);
                error = RpcResult.Ok;
                return true;
            }
            catch (Exception ex)
            {
                error = new RpcResult(RpcRetCode.InitHanderCrash, "An unhandled exception has occurred in Init() method! " + ex.Message);
                return false;
            }
        }

        public void InvokeOnClose()
        {
            try
            {
                MessageHandler.InvokeOnClose();
            }
            catch (Exception ex)
            {
                // TO DO
            }
        }

        private void ProcessOneWayMessage(IMessage message)
        {
            try
            {
                var result = MessageHandler.ProcessMessage(message);
                if (result.IsCompleted)
                {
                    if (result.IsFaulted)
                        OnError(RpcRetCode.MessageHandlerCrash, "Message handler threw an exception: " + result.ToTask().Exception.Message);
                }
                else
                {
                    result.ToTask().ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            OnError(RpcRetCode.MessageHandlerCrash, "Message handler threw an exception: " + t.Exception.Message);
                    });
                }
            }
            catch (Exception ex)
            {
                // TO DO : stop processing here ???
                OnError(RpcRetCode.MessageHandlerCrash, "Message handler threw an exception: " + ex.Message);
            }
        }

        private async void ProcessRequest(IRequestMessage request)
        {
            //var context = new ServiceCallContext(request, Dispatcher);
            var respToSend = await MessageHandler.ProcessRequest(request);
            respToSend.CallId = request.CallId;
            await Tx.TrySendAsync(respToSend);
        }

        private void ProcessResponse(IResponseMessage resp)
        {
            IInteropOperation taskToComplete = null;

            lock (_operations)
            {
                if (_operations.TryGetValue(resp.CallId, out taskToComplete))
                    _operations.Remove(resp.CallId);
                else
                {
                    // TO DO : signal protocol violation
                    return;
                }
            }

            if (resp is IRequestFaultMessage faultMsg)
                taskToComplete.OnFail(faultMsg);
            else
                taskToComplete.OnResponse(resp);
        }

        private void ProcessAxuMessage(IInteropMessage message)
        {
            IInteropOperation operation = null;

            lock (_operations)
            {
                if (!_operations.TryGetValue(message.CallId, out operation))
                {
                    // TO DO : signal protocol violation
                    return;
                }
            }

            var opUpdateResult = operation.OnUpdate(message);

            if (!opUpdateResult.IsOk)
            {
                // TO DO
            }
        }

        internal interface IInteropOperation
        {
            IRequestMessage RequestMessage { get; }

            void StartCancellation();

            RpcResult OnUpdate(IInteropMessage message);
            RpcResult OnResponse(IResponseMessage respMessage);
            void OnFail(RpcResult result);
            void OnFail(IRequestFaultMessage faultMessage);
        }

        public class CallTask<TResp> : TaskCompletionSource<RpcResult>, IInteropOperation
            where TResp : IResponseMessage
        {
            public CallTask(IRequestMessage request)
            {
                RequestMessage = request;
            }

            public string CallId { get; }
            public IRequestMessage RequestMessage { get; }

            public void StartCancellation() { }

            public RpcResult OnResponse(IResponseMessage respMessage)
            {
                SetResult(RpcResult.Ok);
                return RpcResult.Ok;
            }

            public void OnFail(RpcResult result)
            {
                SetException(result.ToException());
            }

            public void OnFail(IRequestFaultMessage faultMessage)
            {
                SetException(faultMessage.CreateException());
            }

            public RpcResult OnUpdate(IInteropMessage page)
            {
                return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
        }

        public class TryCallTask<TResp> : TaskCompletionSource<RpcResult>, IInteropOperation
            where TResp : IResponseMessage
        {
            public TryCallTask(IRequestMessage request)
            {
                RequestMessage = request;
            }

            public IRequestMessage RequestMessage { get; }

            public void StartCancellation() { }

            public RpcResult OnResponse(IResponseMessage respMessage)
            {
                SetResult(RpcResult.Ok);
                return RpcResult.Ok;
            }

            public void OnFail(RpcResult result)
            {
                SetResult(result);
            }

            public void OnFail(IRequestFaultMessage faultMessage)
            {
                SetResult(faultMessage.ToRpcResult());
            }

            public RpcResult OnUpdate(IInteropMessage auxMessage)
            {
                return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
        }

        public class CallTask<TResp, TReturn> : TaskCompletionSource<TReturn>, IInteropOperation
            where TResp : IResponseMessage
        {
            public CallTask(IRequestMessage request)
            {
                RequestMessage = request;
            }

            public IRequestMessage RequestMessage { get; }

            public void StartCancellation() { }

            public RpcResult OnResponse(IResponseMessage respMessage)
            {
                var resp = respMessage as IResponseMessage<TReturn>;
                if (resp != null)
                {
                    SetResult(resp.Result);
                    return RpcResult.Ok;
                }
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }

            public void OnFail(RpcResult result)
            {
                SetException(result.ToException());
            }

            public void OnFail(IRequestFaultMessage faultMessage)
            {
                SetException(faultMessage.CreateException());
            }

            public RpcResult OnUpdate(IInteropMessage auxMessage)
            {
                return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
        }

        public class TryCallTask<TResp, TReturn> : TaskCompletionSource<RpcResult<TReturn>>, IInteropOperation
            where TResp : IResponseMessage
        {
            public TryCallTask(IRequestMessage request)
            {
                RequestMessage = request;
            }

            public IRequestMessage RequestMessage { get; }

            public void StartCancellation() { }

            public RpcResult OnResponse(IResponseMessage respMessage)
            {
                var resp = respMessage as IResponseMessage<TReturn>;
                if (resp != null)
                {
                    SetResult(new RpcResult<TReturn>(resp.Result));
                    return RpcResult.Ok;
                }
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }

            public void OnFail(RpcResult result)
            {
                SetResult(new RpcResult<TReturn>(result.Code, result.FaultMessage, result.CustomFaultData));
            }

            public void OnFail(IRequestFaultMessage faultMessage)
            {
                SetResult(faultMessage.ToRpcResult<TReturn>());
            }

            public RpcResult OnUpdate(IInteropMessage auxMessage)
            {
                return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
        }
    }
}
