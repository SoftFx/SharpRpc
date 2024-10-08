﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using SharpRpc.Streaming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract partial class RxPipeline : IDisposable
    {
        private readonly MessageParser _parser = new MessageParser();
        private readonly RxMessageReader _reader = new RxMessageReader();
        private readonly ByteTransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly MessageDispatcher _msgDispatcher;
        private readonly SessionCoordinator _coordinator;
        private readonly CancellationTokenSource _rxCancelSrc = new CancellationTokenSource();
        private Task _rxLoop;
        private readonly TaskFactory _taskFactory;
        private readonly IRpcLogger _logger;
        private readonly string _channelId;
        private readonly Timer _watchdogTimer;
        private readonly TimeSpan _rxTimeout;

        public RxPipeline(string channelId, ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
        {
            _transport = transport;
            _serializer = serializer;
            _msgDispatcher = messageConsumer;
            _coordinator = coordinator;
            _taskFactory = config.TaskFactory;
            _parser.MaxMessageSize = config.MaxMessageSize;
            _logger = config.GetLogger();
            _channelId = channelId;
            _rxTimeout = config.ReceiveTimeout;
            _watchdogTimer = new Timer(OnWatchdogTick);
        }

        protected MessageDispatcher MessageConsumer => _msgDispatcher;

        public event Action<RpcResult> CommunicationFaulted;

        protected abstract ArraySegment<byte> AllocateRxBuffer();
#if NET5_0_OR_GREATER
        protected abstract ValueTask<bool> OnBytesArrived(int count);
#else
        protected abstract Task<bool> OnBytesArrived(int count);
#endif
        protected abstract void OnCommunicationError(RpcResult fault);

        public abstract void Start();
        public abstract Task Close();

        protected void StartTransportRx()
        {
            _rxLoop = RxLoop();
        }

        protected Task StopTransportRx()
        {
            _rxCancelSrc.Cancel();
            return _rxLoop;
        }

        public void Dispose()
        {
            DisposeWatchdogTimer();
        }

        private async Task RxLoop()
        {
            await _taskFactory.Dive(); // exit lock

            while (true)
            {
                int byteCount;

                try
                {
                    var buffer = AllocateRxBuffer();

                    ResetWatchdogTimer();

                    byteCount = await _transport.Receive(buffer, _rxCancelSrc.Token).ConfigureAwait(false);

                    if (byteCount == 0)
                    {
                        OnCommunicationError(new RpcResult(RpcRetCode.ConnectionAbortedByPeer, "Connection is closed by foreign host."));
                        break;
                    }

                    RegisterBufferRx(byteCount);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (RpcException rex)
                {
                    OnCommunicationError(rex.ToRpcResult());
                    return;
                }
                catch (Exception ex)
                {
                    var fault = _transport.TranslateException(ex);
                    if (fault.Code != RpcRetCode.OperationCanceled)
                        OnCommunicationError(fault);
                    return;
                }

                try
                {
                    await OnBytesArrived(byteCount).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OnCommunicationError(new RpcResult(RpcRetCode.UnknownError, "An exception has occurred in the message parser!", ex.Message));
                    return;
                }
            }
        }

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted?.Invoke(fault);
        }

        protected RpcResult ParseAndDeserialize(ArraySegment<byte> segment, out long bytesConsumed)
        {
            bytesConsumed = 0;

            var container = _msgDispatcher.IncomingMessages;

            _parser.SetNextSegment(segment);

            while (true)
            {
                var pCode = _parser.ParseFurther();

                if (pCode == MessageParser.RetCodes.EndOfSegment)
                {
                    break;
                }
                else if (pCode == MessageParser.RetCodes.MessageParsed)
                {
                    _reader.Init(_parser.MessageBody, _parser.MessageSize);

                    var result = _parser.IsSeMessage ? DeserializeSimplifiedMessage() : DeserializeMessage();

                    if (!result.IsOk)
                        return result;

                    if (result.Value is ISystemMessage sysMsg)
                    {
                        var sysMsgResult = OnSystemMessage(sysMsg);
                        if (sysMsgResult.Code != RpcRetCode.Ok)
                            return sysMsgResult;
                    }
                    else
                    {
                        if (_logger.IsMessageLoggingEnabled)
                        {
                            if (result.Value is IInteropMessage iMsg)
                            {
                                if (!(iMsg is IStreamAuxMessage) || _logger.IsAuxMessageLoggingEnabled)
                                    _logger.Verbose(_channelId, "Recieved " + iMsg.GetMessageName());
                            }
                        }

                        container.Add(result.Value);
                    }

                    bytesConsumed += _parser.MessageBrutto;

                    _reader.Clear();
                }
                else if (pCode == MessageParser.RetCodes.MaxMessageSizeReached)
                    return new RpcResult(RpcRetCode.MaxMessageSizeReached, "An incoming message is too big! Max message size is reached!");
                else
                    return new RpcResult(RpcRetCode.MessageMarkupError, "A violation of message markup has been detected! Code: " + pCode);
            }

            RegisterMessageBatch(container.Count);

            return RpcResult.Ok;
        }

        private RpcResult<IMessage> DeserializeMessage()
        {
            try
            {
                return new RpcResult<IMessage>(_serializer.Deserialize(_reader));
            }
            catch (Exception ex)
            {
                return new RpcResult(RpcRetCode.DeserializationError, ex.JoinAllMessages());
            }
        }

        private RpcResult<IMessage> DeserializeSimplifiedMessage()
        {
            try
            {
                if (!_reader.Se.TryReadByte(out var msgTypeCode))
                    return new RpcResult(RpcRetCode.MessageMarkupError, "");

                if (msgTypeCode == 1)
                {
                    var parseResult = BinaryStreamPage.Read(_reader, out var pageMsg);
                    if (parseResult.IsOk)
                        return new RpcResult<IMessage>(pageMsg);
                    return parseResult;
                }
                else
                    return new RpcResult(RpcRetCode.MessageMarkupError, "");
            }
            catch (Exception ex)
            {
                return new RpcResult(RpcRetCode.DeserializationError, ex.JoinAllMessages());
            }
        }

        private void ResetWatchdogTimer()
        {
            if (_rxTimeout >= TimeSpan.Zero)
                _watchdogTimer.Change(_rxTimeout, Timeout.InfiniteTimeSpan);
        }

        private void DisposeWatchdogTimer()
        {
            _watchdogTimer.Dispose();
        }

        private void OnWatchdogTick(object state)
        {
            SignalCommunicationError(new RpcResult(RpcRetCode.ConnectionTimeout, "No data is received from the transport connection within the specified timeout. This indicates connection loss."));
        }

#if NET5_0_OR_GREATER
        protected ValueTask OnMessagesArrived()
#else
        protected Task OnMessagesArrived()
#endif
        {
            if (_msgDispatcher.IncomingMessages.Count > 0)
                return _msgDispatcher.OnMessages();
            else
                return FwAdapter.AsyncVoid;
        }

        private RpcResult OnSystemMessage(ISystemMessage msg)
        {
            // ignore the heartbeat message (it did its job by just arriving)
            if (msg is IHeartbeatMessage)
            {
                if (_logger.IsAuxMessageLoggingEnabled)
                    _logger.Verbose(_channelId, "Recieved " + msg.GetMessageName());
                return RpcResult.Ok;
            }

            if (_logger.IsMessageLoggingEnabled)
                _logger.Verbose(_channelId, "Recieved " + msg.GetMessageName());

            var result = _coordinator.OnMessage(msg);

            if (result.Code != RpcRetCode.Ok)
                return result;

            //if (isLoggedIn)
            //    _isLoggedIn = true;

            return RpcResult.Ok;
        }

#if PF_COUNTERS
        private long _bufferTotalBytes;
        private int _buffersCount;
        private int _msgCount;
        private int _msgBatchCount;
#endif

        [Conditional("PF_COUNTERS")]
        public void RegisterBufferRx(int bufferSize)
        {
#if PF_COUNTERS
            _bufferTotalBytes += bufferSize;
            _buffersCount++;
#endif
        }

        [Conditional("PF_COUNTERS")]
        public void RegisterMessageBatch(int pageMsgCount)
        {
#if PF_COUNTERS
            _msgCount += pageMsgCount;
            _msgBatchCount++;
#endif
        }

#if PF_COUNTERS
        public int GetPageCount() => _msgBatchCount;
        public double GetAvarageBufferRxSize() => _buffersCount == 0 ? 0 : _bufferTotalBytes / _buffersCount;
        public double GetAvarageMessagesPerBuffer() => _msgBatchCount == 0 ? 0 : _msgCount / _msgBatchCount;
        public double GetAverageMessageSize() => _bufferTotalBytes == 0 ? 0 : _bufferTotalBytes / _msgCount;
        public int GetMessageCount() => _msgCount;
#endif
    }
}
