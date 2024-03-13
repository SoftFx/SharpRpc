// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using SharpRpc.Streaming;
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
        DuplexStreamCall<TInItem, TOutItem>, DuplexStreamCall<TInItem, TOutItem, TReturn>, IDispatcherOperation
    {
        private readonly TaskCompletionSource<RpcResult<TReturn>> _typedCompletion;
        private readonly TaskCompletionSource<RpcResult> _voidCompletion;

        private readonly IStreamWriterFixture<TInItem> _writer;
        private readonly IStreamReaderFixture<TOutItem> _reader;

        private readonly IOpenStreamRequest _requestMessage;
        private readonly IDispatcher _dispatcher;

        private string _name;
        private readonly string _channelId;

        public StreamCall(IOpenStreamRequest request, StreamOptions inputOptions, StreamOptions outputOptions, TxPipeline msgTransmitter,
            IDispatcher dispatcher, IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory,
            bool hasRetParam)
        {
            _requestMessage = request;
            _dispatcher = dispatcher;
            _channelId = msgTransmitter.ChannelId;

            CallId = dispatcher.GenerateOperationId();

            if (inFactory != null)
                _writer = RpcStreams.CreateWriter(CallId, msgTransmitter, inFactory, false, inputOptions, dispatcher.Logger);

            if (outFactory != null)
            {
                _reader = RpcStreams.CreateReader(CallId, msgTransmitter, outFactory, dispatcher.Logger);
                _requestMessage.WindowSize = inputOptions?.WindowSize ?? StreamOptions.DefaultWindowsSize;
            }

            if (hasRetParam)
                _typedCompletion = new TaskCompletionSource<RpcResult<TReturn>>();
            else
                _voidCompletion = new TaskCompletionSource<RpcResult>();

            request.CallId = CallId;
            request.WindowSize = outputOptions?.WindowSize ?? StreamOptions.DefaultWindowsSize;

            var regResult = dispatcher.Register(this);
            if (regResult.IsOk)
            {
                msgTransmitter.TrySendAsync(request, RequestSendCompleted);
                //_canelReg =  cToken.Register(dispatcher.CancelOperation, this);
            }
            else
                Terminate(regResult);
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
                Terminate(result);
        }

        public void Terminate(RpcResult fault)
        {
            _writer?.Terminate(fault);
            _reader?.Terminate(fault);

            EndCall(fault, default);
        }

        private async void EndCall(RpcResult result, TReturn resultValue)
        {
            try
            {
                if (ReturnsResult)
                    _typedCompletion.TrySetResult(result.ToValueResult(resultValue));
                else
                    _voidCompletion.TrySetResult(result);

                if (_reader != null)
                    await _reader.Closed;

                if (_writer != null)
                    await _writer.Closed;

                _dispatcher.Unregister(this);
            }
            catch (Exception ex)
            {
                _dispatcher.Logger.Error(GetName(), ex, "EnsureStreamCloseAndUnregister() failed!");
            }
        }

        #region MessageDispatcherCore.IInteropOperation

        RpcResult IDispatcherOperation.OnResponse(IResponseMessage respMessage)
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

        void IDispatcherOperation.OnFault(RpcResult result)
        {
            EndCall(result, default(TReturn));
        }

        void IDispatcherOperation.OnFaultResponse(IRequestFaultMessage faultMessage)
        {
            EndCall(faultMessage.ToRpcResult(), default(TReturn));
        }

        void IDispatcherOperation.OnRequestCancelled() { }

        RpcResult IDispatcherOperation.OnUpdate(IInteropMessage auxMessage)
        {
            if (_writer != null)
            {
                if (_writer.OnMessage(auxMessage, out var result))
                    return result;
            }

            if (_reader != null)
            {
                if (_reader.OnMessage(auxMessage, out var result))
                    return result;
            }

            return RpcResult.UnexpectedMessage(auxMessage.GetType(), GetType());
        }

        #endregion

        private string GetName()
        {
            if (_name == null)
                _name = $"{_channelId}-SC-{CallId}";
            return _name;
        }
    }
}
