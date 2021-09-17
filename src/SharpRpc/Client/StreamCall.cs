// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface OutputStreamCall<TItem>
    {
        StreamReader<TItem> OutputStream { get; }
        Task<RpcResult> Completion { get; }
    }

    public interface OutputStreamCall<TItem, TReturn>
    {
        StreamReader<TItem> OutputStream { get; }
        Task<RpcResult<TReturn>> AsyncResult { get; }
    }

    public interface InputStreamCall<TItem>
    {
        StreamWriter<TItem> InputStream { get; }
        Task<RpcResult> Completion { get; }
    }

    public interface InputStreamCall<TItem, TReturn>
    {
        StreamWriter<TItem> InputStream { get; }
        Task<RpcResult<TReturn>> AsyncResult { get; }
    }

    public interface DuplexStreamCall<TInItem, TOutItem>
    {
        StreamWriter<TInItem> InputStream { get; }
        StreamReader<TOutItem> OutputStream { get; }
        Task<RpcResult> Completion { get; }
    }

    public interface DuplexStreamCall<TInItem, TOutItem, TReturn>
    {
        StreamReader<TOutItem> OutputStream { get; }
        StreamWriter<TInItem> InputStream { get; }
        Task<RpcResult<TReturn>> AsyncResult { get; }
    }

    internal class StreamCall<TInItem, TOutItem, TReturn> :
        OutputStreamCall<TOutItem>, OutputStreamCall<TOutItem, TReturn>,
        InputStreamCall<TInItem>, InputStreamCall<TInItem, TReturn>,
        DuplexStreamCall<TInItem, TOutItem>, DuplexStreamCall<TInItem, TOutItem, TReturn>,
        MessageDispatcherCore.IInteropOperation
    {
        //private object _stateLockObj = new object();
        private readonly TaskCompletionSource<RpcResult<TReturn>> _typedCompletion;
        private readonly TaskCompletionSource<RpcResult> _voidCompletion;

        private readonly PagingStreamWriter<TInItem> _inputStub;
        private readonly PagingStreamReader<TOutItem> _outputStub;

        private readonly IOpenStreamRequest _requestMessage;

        public StreamCall(IOpenStreamRequest request, Channel ch, IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory,
            bool hasRetParam, CancellationToken cToken)
        {
            _requestMessage = request;

            CallId = ch.Dispatcher.GenerateOperationId(); // Guid.NewGuid().ToString();

            if (inFactory != null)
                _inputStub = new PagingStreamWriter<TInItem>(CallId, ch, inFactory, false, 80, 6);

            if (outFactory != null)
                _outputStub = new PagingStreamReader<TOutItem>(CallId, ch.Tx, outFactory);

            if (hasRetParam)
                _typedCompletion = new TaskCompletionSource<RpcResult<TReturn>>();
            else
                _voidCompletion = new TaskCompletionSource<RpcResult>();

            request.CallId = CallId;

            var regResult = ch.Dispatcher.RegisterCallObject(CallId, this);
            if (regResult.IsOk)
            {
                ch.Tx.TrySendAsync(request, RequestSendCompleted);
                cToken.Register(ch.Dispatcher.CancelOperation, this);
            }
            else
                EndCall(regResult, default(TReturn));
        }

        public string CallId { get; }

        public StreamWriter<TInItem> InputStream => _inputStub;
        public StreamReader<TOutItem> OutputStream => _outputStub;

        public Task<RpcResult> Completion => _voidCompletion.Task;
        public Task<RpcResult<TReturn>> AsyncResult => _typedCompletion.Task;
        public IRequestMessage RequestMessage => _requestMessage;

        private bool ReturnsResult => _typedCompletion != null;

        private void RequestSendCompleted(RpcResult result)
        {
            if (result.IsOk)
                _inputStub?.AllowSend();
            else
                EndCall(result, default(TReturn));
        }

        private void EndCall(RpcResult result, TReturn resultValue)
        {
            _inputStub?.Close(result);

            if (ReturnsResult)
                _typedCompletion.TrySetResult(result.ToValueResult(resultValue));
            else
                _voidCompletion.TrySetResult(result);
        }

        #region MessageDispatcherCore.IInteropOperation

        RpcResult MessageDispatcherCore.IInteropOperation.Complete(IResponseMessage respMessage)
        {
            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " RESP " + respMessage.GetType().Name);

            if (ReturnsResult)
            {
                var resp = respMessage as IResponseMessage<TReturn>;
                if (resp != null)
                {
                    _typedCompletion.TrySetResult(new RpcResult<TReturn>(resp.Result));
                    return RpcResult.Ok;
                }
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
            else
            {
                _voidCompletion.TrySetResult(RpcResult.Ok);
                return RpcResult.Ok;
            }
        }

        void MessageDispatcherCore.IInteropOperation.Fail(RpcResult result)
        {
            if (ReturnsResult)
                _typedCompletion.TrySetResult(result.ToValueResult<TReturn>());
            else
                _voidCompletion.TrySetResult(result);
        }

        void MessageDispatcherCore.IInteropOperation.Fail(IRequestFaultMessage faultMessage)
        {
            if (ReturnsResult)
                _typedCompletion.TrySetResult(faultMessage.ToRpcResult<TReturn>());
            else
                _voidCompletion.TrySetResult(faultMessage.ToRpcResult());
        }

        RpcResult MessageDispatcherCore.IInteropOperation.Update(IInteropMessage auxMessage)
        {
            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " A.MSG " + auxMessage.GetType().Name);

            if (auxMessage is IStreamPage<TOutItem> page)
            {
                _outputStub.OnRx(page);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamCompletionMessage compl)
            {
                _outputStub.OnRx(compl);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamPageAck ack)
            {
                _inputStub.OnRx(ack);
                return RpcResult.Ok;
            }

            return new RpcResult(RpcRetCode.ProtocolViolation, "");
        }

        void MessageDispatcherCore.IInteropOperation.StartCancellation()
        {
            
        }

        #endregion
    }
}
