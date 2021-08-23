// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
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

        public TxTransportFeed(TxBuffer queue, Action<RpcResult> comErrorHandler)
        {
            _queue = queue;
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
            await Task.Factory.Dive();

            try
            {
                while (true)
                {
                    var data = await _queue.DequeueNext();

                    if (data.Array == null)
                        return;

                    //await Task.Yield();

                    try
                    {
                        await _trasport.Send(data, _txCancelSrc.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // normal exit
                        return;
                    }
                    catch (Exception ex)
                    {
                        var fault = _trasport.TranslateException(ex);
                        _comErrorHandler(fault);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _comErrorHandler(new RpcResult(RpcRetCode.OtherError, ex.Message));
            }
        }

        //internal interface IByteQueue
        //{
        //    ArraySegment<byte> DequeueNextSegment();
        //}
    }
}
