// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    internal class BinaryStreamWriter : StreamWriterBase2<byte>
    {
        private ArraySegment<byte> _queue;
        private ArraySegment<byte> _bufferToSend;
        private readonly bool _canImmediatelyReusePages;

        public BinaryStreamWriter(string callId, TxPipeline msgTransmitter, IStreamMessageFactory factory,
            bool allowSending, StreamOptions options, IRpcLogger logger)

            : base(callId, msgTransmitter, factory, allowSending, options, logger)
        {
            _canImmediatelyReusePages = Tx.ImmediateSerialization;

            _queue = AllocatePage();
            if (_canImmediatelyReusePages)
                _bufferToSend = AllocatePage();
        }

        public override int QueueSize => _queue.Count;

        protected override bool DataIsAvailable => _queue.Count > 0;
        protected override bool HasSpaceInQueue => _queue.Count < MaxPageSize;

        protected override void DropQueue()
        {
            _queue = new ArraySegment<byte>(_queue.Array);
        }

        protected override void EnqueueItem(byte item)
        {
            var index = _queue.Offset + _queue.Count;
            _queue.Array[index] = item;
            _queue = new ArraySegment<byte>(_queue.Array, _queue.Offset, _queue.Count + 1);
        }

        protected override void FillSendBuffer()
        {
            if (_canImmediatelyReusePages)
            {
                var arrayToReuse = _bufferToSend.Array;
                _bufferToSend = _queue;
                _queue = new ArraySegment<byte>(arrayToReuse, 0, 0);
            }
            else
            {
                _queue = AllocatePage();
                _bufferToSend = _queue;
            }
        }

        protected override void FreeSendBuffer(out int sentDataSize)
        {
            sentDataSize = _bufferToSend.Count;

            if (!_canImmediatelyReusePages)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(_bufferToSend.Array);
                _bufferToSend = default;
            }
                
        }

        protected override bool OnMessage(IInteropMessage auxMessage, out RpcResult result)
        {
            result = RpcResult.Ok;

            if (auxMessage is IStreamPageAck pageAck)
            {
                OnRx(pageAck);
                return true;
            }
            else if (auxMessage is IStreamCloseAckMessage closeAckMsg)
            {
                result = OnRx(closeAckMsg);
                return true;
            }
            else if (auxMessage is IStreamCancelMessage cancelMessage)
            {
                OnRx(cancelMessage);
                return true;
            }

            return false;
        }

        protected override void SendNextPage()
        {
            Tx.TrySendBytePage(CallId, _bufferToSend, OnPageSendCompleted);
        }

        protected ArraySegment<byte> AllocatePage()
        {
            return new ArraySegment<byte>(System.Buffers.ArrayPool<byte>.Shared.Rent(MaxPageSize), 0, 0);
        }

        protected override ArraySegment<byte> ReserveBulkWriteBuffer()
        {
            var availableSpace = MaxPageSize - _queue.Count;
            return new ArraySegment<byte>(_queue.Array, _queue.Offset + _queue.Count, availableSpace);
        }

        protected override void CommitBulkWriteBuffer(int writeSize)
        {
            _queue = new ArraySegment<byte>(_queue.Array, _queue.Offset, _queue.Count + writeSize);
        }
    }
}
