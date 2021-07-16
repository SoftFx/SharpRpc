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
    partial class RxPipeline
    {
        public class NoThreading : RxPipeline
        {
            private readonly RxBuffer _buffer;
            private volatile bool _isClosed;

            public NoThreading(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer, SessionCoordinator coordinator)
                : base(transport, config, serializer, messageConsumer, coordinator)
            {
                _buffer = new RxBuffer(config.RxBufferSegmentSize);
            }

            public override void Start()
            {
                StartTransportRx();
            }

            protected override void OnCommunicationError(RpcResult fault)
            {
                SignalCommunicationError(fault);
            }

            protected override ArraySegment<byte> AllocateRxBuffer()
            {
                return _buffer.GetRxSegment();
            }

#if NET5_0_OR_GREATER
            protected async override ValueTask<bool> OnBytesArrived(int count)
#else
            protected async override Task<bool> OnBytesArrived(int count)
#endif
            {
                if (_isClosed)
                    return false;

                var rxData = _buffer.CommitDataRx(count);
                var parseRet = ParseAndDeserialize(rxData, out var bytesConsumed);

                if (parseRet.Code != RpcRetCode.Ok)
                {
                    SignalCommunicationError(parseRet);
                    return false;
                }

                await SubmitParsedBatch();
                _msgDispatcher.IncomingMessages.Clear();

                _buffer.CommitDataConsume(bytesConsumed);

                return true;
            }

            public override Task Close()
            {
                _isClosed = true;

                return StopTransportRx();
            }
        }
    }
}
