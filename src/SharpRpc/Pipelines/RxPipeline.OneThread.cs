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
using System.Threading.Tasks;

namespace SharpRpc
{
    partial class RxPipeline
    {
        //        public class OneThread : RxPipeline
        //        {
        //            private object _lockObj = new object();
        //            private readonly RxBuffer _buffer;
        //            private bool _isClosing;
        //            private TaskCompletionSource<bool> _enqeueuWaitHandler;
        //            private TaskCompletionSource<bool> _closeWaitHandler;
        //            private bool _isBusy;
        //            private ArraySegment<byte> _segmentToParse;
        //            private ArraySegment<byte> _awaitingSegment;
        //            private RpcResult _communicationError;

        //            public OneThread(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
        //                : base(transport, config, serializer, messageConsumer, coordinator)
        //            {
        //                _buffer = new RxBuffer(config.RxBufferSegmentSize);
        //            }

        //            protected override ArraySegment<byte> AllocateRxBuffer()
        //            {
        //                lock (_lockObj)
        //                    return _buffer.GetRxSegment();
        //            }

        //#if NET5_0_OR_GREATER
        //            protected override ValueTask<bool> OnBytesArrived(int count)
        //#else
        //            protected override Task<bool> OnBytesArrived(int count)
        //#endif
        //            {
        //                lock (_lockObj)
        //                {
        //                    if (_isClosing)
        //                        return FwAdapter.AsyncFalse;

        //                    var arrivedData = _buffer.CommitDataRx(count);

        //                    if (_isBusy)
        //                    {
        //                        Debug.Assert(_enqeueuWaitHandler == null);

        //                        _awaitingSegment = arrivedData;
        //                        _enqeueuWaitHandler = new TaskCompletionSource<bool>();
        //                        return FwAdapter.WrappResult(_enqeueuWaitHandler.Task);
        //                    }
        //                    else
        //                    {
        //                        LaunchWorker(arrivedData);
        //                        return FwAdapter.AsyncTrue;
        //                    }
        //                }
        //            }

        //            protected override void OnCommunicationError(RpcResult fault)
        //            {
        //                bool signalNow;

        //                lock (_lockObj)
        //                {
        //                    _communicationError = fault;
        //                    signalNow = !_isBusy;
        //                }

        //                if (signalNow)
        //                    SignalCommunicationError(fault);
        //            }

        //            private void LaunchWorker(ArraySegment<byte> dataToProcess)
        //            {
        //                _isBusy = true;

        //                _segmentToParse = dataToProcess;
        //                //Task.Factory.StartNew(ParseSegment);
        //                ParseSegment();
        //            }

        //            public override void Start()
        //            {
        //                StartTransportRx();
        //            }

        //            public override Task Close()
        //            {
        //                lock (_lockObj)
        //                {
        //                    _isClosing = true;

        //                    if (!_isBusy)
        //                    {
        //                        CompleteClose();
        //                        return StopTransportRx();
        //                    }
        //                    else
        //                    {
        //                        _closeWaitHandler = new TaskCompletionSource<bool>();
        //                        return Task.WhenAll(_closeWaitHandler.Task, StopTransportRx());
        //                    }
        //                }
        //            }

        //            private async void ParseSegment()
        //            {
        //                // occupy a separate thread
        //                await _taskQueue.Dive();

        //                var parseRes = ParseAndDeserialize(_segmentToParse, out var bytesConsumed);

        //                if (parseRes.Code == RpcRetCode.Ok)
        //                {
        //                    await SubmitParsedBatch().ConfigureAwait(false);
        //                    _msgDispatcher.IncomingMessages.Clear();
        //                    OnParseCompleted(bytesConsumed);
        //                }
        //                else
        //                    OnParseFailed(parseRes);       
        //            }

        //            private void OnParseCompleted(long dataSize)
        //            {
        //                TaskCompletionSource<bool> toSignal = null;
        //                bool isNotClosed;

        //                lock (_lockObj)
        //                {
        //                    _isBusy = false;
        //                    _segmentToParse = default;

        //                    _buffer.CommitDataConsume(dataSize);

        //                    if (_enqeueuWaitHandler != null)
        //                    {
        //                        toSignal = _enqeueuWaitHandler;
        //                        _enqeueuWaitHandler = null;

        //                        if (!_isClosing)
        //                        {
        //                            LaunchWorker(_awaitingSegment);
        //                            _awaitingSegment = default;
        //                        }
        //                    }

        //                    if (_isClosing)
        //                        CompleteClose();
        //                    else if (!_communicationError.IsOk)
        //                        SignalCommunicationError(_communicationError);

        //                    isNotClosed = !_isClosing;
        //                }

        //                toSignal?.SetResult(isNotClosed);
        //            }

        //            private void OnParseFailed(RpcResult result)
        //            {
        //                TaskCompletionSource<bool> toSignal = null;

        //                lock (_lockObj)
        //                {
        //                    _isClosing = true;
        //                    _isBusy = false;

        //                    toSignal = _enqeueuWaitHandler;
        //                    _enqeueuWaitHandler = null;
        //                }

        //                toSignal?.SetResult(false);

        //                SignalCommunicationError(result);
        //            }

        //            private void CompleteClose()
        //            {
        //                _buffer.Dispose();
        //                _closeWaitHandler?.TrySetResult(true);
        //            }
        //        }
    }
}
