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
        private readonly Dictionary<string, ITask> _callTasks = new Dictionary<string, ITask>();
        private bool _completed;
        private RpcResult _fault;
        private List<ITask> _tasksToCancel;

        public MessageDispatcherCore(TxPipeline txNode, IUserMessageHandler msgHandler, Action<RpcRetCode, string> onErrorAction)
        {
            Tx = txNode;
            MessageHandler = msgHandler;
            OnError = onErrorAction;
        }

        public TxPipeline Tx { get; }
        public IUserMessageHandler MessageHandler { get; }
        public Action<RpcRetCode, string> OnError { get; }
        

        public RpcResult TryRegisterTask(string callId, ITask callTask)
        {
            lock (_callTasks)
            {
                if (_completed)
                    return _fault;

                _callTasks.Add(callId, callTask);

                return RpcResult.Ok;
            }
        }

        public bool UnregisterTask(string callId)
        {
            lock (_callTasks)
                return _callTasks.Remove(callId);
        }

        public void ProcessMessage(IMessage message)
        {
            if (message is IReqRespMessage)
            {
                if (message is IResponse resp)
                    ProcessResponse(resp);
                else if (message is IRequest req)
                    ProcessRequest(req);
            }
            else
                ProcessOneWayMessage(message);
        }

        public void OnStop(RpcResult fault)
        {
            lock (_callTasks)
            {
                _completed = true;
                _fault = fault;

                _tasksToCancel = _callTasks.Values.ToList();
                _callTasks.Clear();
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
                return new RpcResult(RpcRetCode.RequestCrashed, "An exception has been occured in ");
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
                        OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + result.ToTask().Exception.Message);
                }
                else
                {
                    result.ToTask().ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + t.Exception.Message);
                    });
                }
            }
            catch (Exception ex)
            {
                // TO DO : stop processing here ???
                OnError(RpcRetCode.MessageHandlerFailure, "Message handler threw an exception: " + ex.Message);
            }
        }

        private async void ProcessRequest(IRequest request)
        {
            var respToSend = await MessageHandler.ProcessRequest(request);
            respToSend.CallId = request.CallId;
            await Tx.TrySendAsync(respToSend);
        }

        private void ProcessResponse(IResponse resp)
        {
            ITask taskToComplete = null;

            lock (_callTasks)
            {
                if (_callTasks.TryGetValue(resp.CallId, out taskToComplete))
                    _callTasks.Remove(resp.CallId);
                else
                {
                    // TO DO : signal protocol violation
                    return;
                }
            }

            if (resp is IRequestFault faultMsg)
                taskToComplete.Fail(faultMsg);
            else
                taskToComplete.Complete(resp);
        }

        public interface ITask
        {
            void Complete(IResponse respMessage);
            void Fail(RpcResult result);
            void Fail(IRequestFault faultMessage);
        }

        public class CallTask<TResp> : TaskCompletionSource<RpcResult>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                SetResult(RpcResult.Ok);
            }

            public void Fail(RpcResult result)
            {
                SetException(result.ToException());
            }

            public void Fail(IRequestFault faultMessage)
            {
                SetException(faultMessage.CreateException());
            }
        }

        public class TryCallTask<TResp> : TaskCompletionSource<RpcResult>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                SetResult(RpcResult.Ok);
            }

            public void Fail(RpcResult result)
            {
                SetResult(result);
            }

            public void Fail(IRequestFault faultMessage)
            {
                var result = new RpcResult(faultMessage.Code.ToRetCode(), faultMessage.GetFault());
                SetResult(result);
            }
        }

        public class CallTask<TResp, TReturn> : TaskCompletionSource<TReturn>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                var result = ((IResponse<TReturn>)respMessage).Result;
                SetResult(result);
            }

            public void Fail(RpcResult result)
            {
                SetException(result.ToException());
            }

            public void Fail(IRequestFault faultMessage)
            {
                SetException(faultMessage.CreateException());
            }
        }

        public class TryCallTask<TResp, TReturn> : TaskCompletionSource<RpcResult<TReturn>>, ITask
            where TResp : IResponse
        {
            public void Complete(IResponse respMessage)
            {
                var result = ((IResponse<TReturn>)respMessage).Result;
                SetResult(new RpcResult<TReturn>(result));
            }

            public void Fail(RpcResult result)
            {
                SetResult(new RpcResult<TReturn>(result.Code, result.Fault));
            }

            public void Fail(IRequestFault faultMessage)
            {
                var result = new RpcResult<TReturn>(faultMessage.Code.ToRetCode(), faultMessage.GetFault());
                SetResult(result);
            }
        }
    }
}
