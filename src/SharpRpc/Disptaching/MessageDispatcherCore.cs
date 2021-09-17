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

        public MessageDispatcherCore(TxPipeline txNode, MessageDispatcher dispatcher, IUserMessageHandler msgHandler, Action<RpcRetCode, string> onErrorAction)
        {
            Tx = txNode;
            Dispatcher = dispatcher;
            MessageHandler = msgHandler;
            OnError = onErrorAction;
        }

        public TxPipeline Tx { get; }
        public MessageDispatcher Dispatcher { get; }
        public IUserMessageHandler MessageHandler { get; }
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
                task.Fail(_fault);
        }

        public RpcResult FireOpened()
        {
            try
            {
                ((RpcCallHandler)MessageHandler).Session.FireOpened(new SessionOpenedEventArgs());
            }
            catch (Exception)
            {
                // TO DO : log or pass some more information about expcetion (stack trace)
                return new RpcResult(RpcRetCode.RequestCrash, "An exception has been occured in ");
            }

            return RpcResult.Ok;
        }

        public void FireClosed()
        {
            try
            {
                ((RpcCallHandler)MessageHandler).Session.FireClosed(new SessionClosedEventArgs());
            }
            catch (Exception)
            {
                // TO DO : log or pass some more information about expcetion (stack trace)
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
                taskToComplete.Fail(faultMsg);
            else
                taskToComplete.Complete(resp);
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

            var opUpdateResult = operation.Update(message);

            if (!opUpdateResult.IsOk)
            {
                // TO DO
            }
        }

        internal interface IInteropOperation
        {
            IRequestMessage RequestMessage { get; }

            void StartCancellation();

            RpcResult Update(IInteropMessage message);
            RpcResult Complete(IResponseMessage respMessage);
            void Fail(RpcResult result);
            void Fail(IRequestFaultMessage faultMessage);
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

            public RpcResult Complete(IResponseMessage respMessage)
            {
                SetResult(RpcResult.Ok);
                return RpcResult.Ok;
            }

            public void Fail(RpcResult result)
            {
                SetException(result.ToException());
            }

            public void Fail(IRequestFaultMessage faultMessage)
            {
                SetException(faultMessage.CreateException());
            }

            public RpcResult Update(IInteropMessage page)
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

            public RpcResult Complete(IResponseMessage respMessage)
            {
                SetResult(RpcResult.Ok);
                return RpcResult.Ok;
            }

            public void Fail(RpcResult result)
            {
                SetResult(result);
            }

            public void Fail(IRequestFaultMessage faultMessage)
            {
                SetResult(faultMessage.ToRpcResult());
            }

            public RpcResult Update(IInteropMessage auxMessage)
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

            public RpcResult Complete(IResponseMessage respMessage)
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

            public void Fail(RpcResult result)
            {
                SetException(result.ToException());
            }

            public void Fail(IRequestFaultMessage faultMessage)
            {
                SetException(faultMessage.CreateException());
            }

            public RpcResult Update(IInteropMessage auxMessage)
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

            public RpcResult Complete(IResponseMessage respMessage)
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

            public void Fail(RpcResult result)
            {
                SetResult(new RpcResult<TReturn>(result.Code, result.FaultMessage, result.CustomFaultData));
            }

            public void Fail(IRequestFaultMessage faultMessage)
            {
                SetResult(faultMessage.ToRpcResult<TReturn>());
            }

            public RpcResult Update(IInteropMessage auxMessage)
            {
                return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
        }
    }
}
