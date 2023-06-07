// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract partial class RxPipeline
    {
        private readonly MessageParser _parser = new MessageParser();
        private readonly RxMessageReader _reader = new RxMessageReader();
        private readonly ByteTransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly MessageDispatcher _msgDispatcher;
        //private readonly List<IMessage> _page = new List<IMessage>();
        //private readonly List<IMessage> _oneWayMsgPage = new List<IMessage>();
        private readonly SessionCoordinator _coordinator;
        private readonly CancellationTokenSource _rxCancelSrc = new CancellationTokenSource();
        private Task _rxLoop;
        private readonly TaskFactory _taskQueue;

        public RxPipeline(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
        {
            _transport = transport;
            _serializer = serializer;
            _msgDispatcher = messageConsumer;
            _coordinator = coordinator;
            _taskQueue = config.TaskQueue;
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

        private async Task RxLoop()
        {
            while (true)
            {
                int byteCount;

                try
                {
                    var buffer = AllocateRxBuffer();

                    byteCount = await _transport.Receive(buffer, _rxCancelSrc.Token);

                    //await _taskQueue.Dive();

                    if (byteCount == 0)
                    {
                        OnCommunicationError(new RpcResult(RpcRetCode.ConnectionAbortedByPeer, "Connection is closed by foreign host."));
                        break;
                    }

                    RegisterDataRx(byteCount);
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
                    OnCommunicationError(fault);
                    return;
                }

                await OnBytesArrived(byteCount);

                //if (!await OnBytesArrived(byteCount))
                //    break;
            }
        }

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted?.Invoke(fault);
        }

        protected RpcResult ParseAndDeserialize(ArraySegment<byte> segment, out int bytesConsumed)
        {
            bytesConsumed = 0;

            var container = _msgDispatcher.IncomingMessages;

            //_page.Clear();
            //_oneWayMsgPage.Clear();
            _parser.SetNextSegment(segment);

            while (true)
            {
                var pCode = _parser.ParseFurther();

                if (pCode == MessageParser.RetCodes.EndOfSegment)
                    break;
                else if (pCode == MessageParser.RetCodes.MessageParsed)
                {
                    _reader.Init(_parser.MessageBody);

                    try
                    {
                        var msg = _serializer.Deserialize(_reader);

                        //Debug.WriteLine("RX " + msg.GetType().Name);

                        if (msg is ISystemMessage sysMsg)
                        {
                            var sysMsgResult = OnSystemMessage(sysMsg);
                            if (sysMsgResult.Code != RpcRetCode.Ok)
                                return sysMsgResult;
                        }
                        else
                        {
                            //if (msg is ISystemMessage || (msg is IInteropMessage && !(msg is IStreamPage)))
                                container.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        return new RpcResult(RpcRetCode.DeserializationError, ex.JoinAllMessages());
                    }

                    bytesConsumed += _parser.MessageBrutto;

                    _reader.Clear();
                }
                else
                    return new RpcResult(RpcRetCode.MessageMarkupError, "A violation of message markup has been detected! Code: " + pCode);
            }

            RegisterMessagePage(container.Count);

            return RpcResult.Ok;
        }

#if NET5_0_OR_GREATER
        protected ValueTask SubmitParsedBatch()
#else
        protected Task SubmitParsedBatch()
#endif
        {
            if (_msgDispatcher.IncomingMessages.Count > 0)
                return _msgDispatcher.OnMessages();
            else
                return FwAdapter.AsyncVoid;
        }

        private RpcResult OnSystemMessage(ISystemMessage msg)
        {
            // ignore hartbeat (it did his job by just arriving)
            if (msg is IHeartbeatMessage)
                return RpcResult.Ok;

            var result = _coordinator.OnMessage(msg);

            if (result.Code != RpcRetCode.Ok)
                return result;

            //if (isLoggedIn)
            //    _isLoggedIn = true;

            return RpcResult.Ok;
        }


#if PF_COUNTERS
        private int _totalBytes;
        private int _byteChunkCount;
        private int _msgCount;
        private int _pageCount;
#endif

        [Conditional("PF_COUNTERS")]
        public void RegisterDataRx(int bufferSize)
        {
#if PF_COUNTERS
            _totalBytes += bufferSize;
            _byteChunkCount++;
#endif
        }

        [Conditional("PF_COUNTERS")]
        public void RegisterMessagePage(int pageMsgCount)
        {
#if PF_COUNTERS
            _msgCount += pageMsgCount;
            _pageCount++;
#endif
        }

#if PF_COUNTERS
        public int GetPageCount() => _pageCount;
        public double GetAvarageRxSize() => _byteChunkCount == 0 ? 0 : _totalBytes / _byteChunkCount;
        public double GetAvaragePageSize() => _pageCount == 0 ? 0 : _msgCount / _pageCount;
#endif
    }
}
