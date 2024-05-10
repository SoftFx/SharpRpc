// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class TxTransportFeed
    {
        private ByteTransport _trasport;
        private Task _txLoop;
        private readonly CancellationTokenSource _txCancelSrc = new CancellationTokenSource();
        private readonly TxBuffer _queue;
        private readonly Action<RpcResult> _comErrorHandler;
        private readonly TaskFactory _taskFactory;

        public TxTransportFeed(TxBuffer queue, TaskFactory tFactory, Action<RpcResult> comErrorHandler)
        {
            _queue = queue;
            _taskFactory = tFactory;
            _comErrorHandler = comErrorHandler;
        }

        public void StartTransportWrite(ByteTransport transport)
        {
            _trasport = transport;
            _txLoop = TxBytesLoop();
        }

        public void AbortTransportWriteAfter(TimeSpan timeSpan)
        {
            _txCancelSrc.CancelAfter(timeSpan);
        }

        public void AbortTransportWrite()
        {
            _txCancelSrc.Cancel();
        }

        public Task WaitTransportWaitToEnd()
        {
            return _txLoop ?? Task.CompletedTask;
        }

        private async Task TxBytesLoop()
        {
            // tak another thread (and exit lock)
            await _taskFactory.Dive();

            try
            {
                while (true)
                {
                    UpdateState(LoopState.Dequeue);

                    var data = await _queue.DequeueNext();

                    if (data.Array == null)
                    {
                        // normal exit
                        return;
                    }

                    try
                    {
                        UpdateState(LoopState.Write);

                        await _trasport.Send(data, _txCancelSrc.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        UpdateState(LoopState.Closed);
                        // loop was canceled
                        return;
                    }
                    catch (Exception ex)
                    {
                        var fault = _trasport.TranslateException(ex);
                        _comErrorHandler(fault);
                        UpdateState(LoopState.Closed);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _comErrorHandler(new RpcResult(RpcRetCode.OtherError, ex.Message));
                UpdateState(LoopState.Closed);
            }
        }

#if DEBUG
        private LoopState _state;
#endif

        private enum LoopState { None, Dequeue, Write, Closed }

        [Conditional("DEBUG")]
        private void UpdateState(LoopState state)
        {
#if DEBUG
            _state = state;
#endif
        }
    }
}
