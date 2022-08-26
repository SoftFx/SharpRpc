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
        private readonly TaskCompletionSource<RpcResult<TReturn>> _typedCompletion;
        private readonly TaskCompletionSource<RpcResult> _voidCompletion;

        private readonly PagingStreamWriter<TInItem> _writer;
        private readonly PagingStreamReader<TOutItem> _reader;

        private readonly IOpenStreamRequest _requestMessage;

        public StreamCall(IOpenStreamRequest request, StreamOptions inputOptions, StreamOptions outputOptions, TxPipeline msgTransmitter,
            IOpDispatcher dispatcher, IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory,
            bool hasRetParam, CancellationToken cToken)
        {
            _requestMessage = request;

            CallId = dispatcher.GenerateOperationId();

            if (inFactory != null)
                _writer = new PagingStreamWriter<TInItem>(CallId, msgTransmitter, inFactory, false, inputOptions);

            if (outFactory != null)
            {
                _reader = new PagingStreamReader<TOutItem>(CallId, msgTransmitter, outFactory);
                _requestMessage.WindowSize = inputOptions?.WindowSize ?? StreamOptions.DefaultWindowsSize;
            }

            if (hasRetParam)
                _typedCompletion = new TaskCompletionSource<RpcResult<TReturn>>();
            else
                _voidCompletion = new TaskCompletionSource<RpcResult>();

            request.CallId = CallId;
            request.WindowSize = outputOptions?.WindowSize ?? StreamOptions.DefaultWindowsSize;

            if (cToken.CanBeCanceled)
                request.Options |= RequestOptions.CancellationEnabled;

            var regResult = dispatcher.RegisterCallObject(CallId, this);
            if (regResult.IsOk)
            {
                msgTransmitter.TrySendAsync(request, RequestSendCompleted);
                //_canelReg =  cToken.Register(dispatcher.CancelOperation, this);
            }
            else
                EndCall(regResult, default(TReturn));
        }

        public string CallId { get; }

        public StreamWriter<TInItem> InputStream => _writer;
        public StreamReader<TOutItem> OutputStream => _reader;

        public Task<RpcResult> Completion => _voidCompletion.Task;
        public Task<RpcResult<TReturn>> AsyncResult => _typedCompletion.Task;
        public IRequestMessage RequestMessage => _requestMessage;

        private bool ReturnsResult => _typedCompletion != null;

        private void RequestSendCompleted(RpcResult result)
        {
            if (result.IsOk)
                _writer?.AllowSend();
            else
                EndCall(result, default(TReturn));
        }

        private void EndCall(RpcResult result, TReturn resultValue)
        {
            _writer?.Close(result);

            if (ReturnsResult)
                _typedCompletion.TrySetResult(result.ToValueResult(resultValue));
            else
                _voidCompletion.TrySetResult(result);

            //_canelReg.Dispose();

            if (result.IsOk)
            {
                _writer?.Abort(new RpcResult(RpcRetCode.StreamCompleted, "Stream is closed due to call completion."));
                _reader?.Complete();
            }
            else
            {
                _writer?.Abort(result);
                _reader?.Abort(result);
            }
        }

        #region MessageDispatcherCore.IInteropOperation

        RpcResult MessageDispatcherCore.IInteropOperation.OnResponse(IResponseMessage respMessage)
        {
            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " RESP " + respMessage.GetType().Name);

            if (ReturnsResult)
            {
                var resp = respMessage as IResponseMessage<TReturn>;
                if (resp != null)
                {
                    EndCall(RpcResult.Ok, resp.Result);
                    return RpcResult.Ok;
                }
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
            }
            else
            {
                EndCall(RpcResult.Ok, default(TReturn));
                return RpcResult.Ok;
            }
        }

        void MessageDispatcherCore.IInteropOperation.OnFail(RpcResult result)
        {
            EndCall(result, default(TReturn));
        }

        void MessageDispatcherCore.IInteropOperation.OnFail(IRequestFaultMessage faultMessage)
        {
            EndCall(faultMessage.ToRpcResult(), default(TReturn));
        }

        RpcResult MessageDispatcherCore.IInteropOperation.OnUpdate(IInteropMessage auxMessage)
        {
            //System.Diagnostics.Debug.WriteLine("RX " + CallId + " A.MSG " + auxMessage.GetType().Name);

            if (auxMessage is IStreamPage<TOutItem> page)
            {
                if (_reader == null)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
                _reader.OnRx(page);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamCompletionMessage compl)
            {
                if (_reader == null)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
                _reader.OnRx(compl);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamCompletionRequestMessage complRequest)
            {
                if (_writer == null)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
                _writer.OnRx(complRequest);
                return RpcResult.Ok;
            }
            else if (auxMessage is IStreamPageAck ack)
            {
                if (_writer == null)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "");
                _writer.OnRx(ack);
                return RpcResult.Ok;
            }

            return new RpcResult(RpcRetCode.ProtocolViolation, "");
        }

        void MessageDispatcherCore.IInteropOperation.StartCancellation()
        {
            throw new NotSupportedException("Stream calls do not support standard operation cancellation.");
        }

        #endregion
    }
}
