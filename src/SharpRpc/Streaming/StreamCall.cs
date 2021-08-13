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
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface OutputStreamCall<TItem>
    {
        PagingTxStream<TItem> OutputStream { get; }
        Task<RpcResult> Completion { get; }
    }

    public interface OutputStreamCall<TItem, TReturn>
    {
        PagingTxStream<TItem> OutputStream { get; }
        Task<RpcResult<TReturn>> AsyncResult { get; }
    }

    public interface InputStreamCall<TItem>
    {
        PagingRxStream<TItem> InputStream { get; }
        Task<RpcResult> Completion { get; }
    }

    public interface InputStreamCall<TItem, TReturn>
    {
        PagingRxStream<TItem> InputStream { get; }
        Task<RpcResult<TReturn>> AsyncResult { get; }
    }

    public interface DuplexStreamCall<TInItem, TOutItem> : InputStreamCall<TInItem>, OutputStreamCall<TOutItem>
    {
    }

    public interface DuplexStreamCall<TInItem, TOutItem, TReturn> : InputStreamCall<TInItem, TReturn>, OutputStreamCall<TOutItem, TReturn>
    {
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

        public StreamCall(IOpenStreamRequest request, Channel ch, IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory, bool hasRetParam)
        {
            CallId = Guid.NewGuid().ToString();

            if (inFactory != null)
                InputStream = new PagingRxStream<TInItem>(inFactory);

            if (outFactory != null)
                OutputStream = new PagingTxStream<TOutItem>(CallId, ch, outFactory, 10, 10);

            if (hasRetParam)
                _typedCompletion = new TaskCompletionSource<RpcResult<TReturn>>();
            else
                _voidCompletion = new TaskCompletionSource<RpcResult>();

            request.CallId = CallId;

            var regResult = ch.Dispatcher.RegisterCallObject(CallId, this);
            if (regResult.IsOk)
                ch.Tx.TrySendAsync(request, RequestSendCompleted);
            else
                EndCall(regResult, default(TReturn));
        }

        public string CallId { get; }

        public PagingRxStream<TInItem> InputStream { get; }
        public PagingTxStream<TOutItem> OutputStream { get; }

        public Task<RpcResult> Completion => _voidCompletion.Task;
        public Task<RpcResult<TReturn>> AsyncResult => _typedCompletion.Task;

        private bool ReturnsResult => _typedCompletion != null;

        private void RequestSendCompleted(RpcResult result)
        {
            if (result.IsOk)
                OutputStream?.AllowSend();
            else
                EndCall(result, default(TReturn));
        }

        private void EndCall(RpcResult result, TReturn resultValue)
        {
            //InputStream?.clos
            OutputStream?.Close(result);

            if (ReturnsResult)
                _typedCompletion.TrySetResult(result.ToValueResult(resultValue));
            else
                _voidCompletion.TrySetResult(result);
        }

        #region MessageDispatcherCore.IInteropOperation

        RpcResult MessageDispatcherCore.IInteropOperation.Complete(IResponse respMessage)
        {
            if (ReturnsResult)
            {
                var resp = respMessage as IResponse<TReturn>;
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

        void MessageDispatcherCore.IInteropOperation.Fail(IRequestFault faultMessage)
        {
            if (ReturnsResult)
            {
                var result = new RpcResult<TReturn>(faultMessage.Code.ToRetCode(), faultMessage.GetFault());
                _typedCompletion.TrySetResult(result);
            }
            else
            {
                var result = new RpcResult(faultMessage.Code.ToRetCode(), faultMessage.GetFault());
                _voidCompletion.TrySetResult(result);
            }
        }

        RpcResult MessageDispatcherCore.IInteropOperation.Update(IStreamPage page)
        {
            return RpcResult.Ok;
        }

        #endregion
    }
}
