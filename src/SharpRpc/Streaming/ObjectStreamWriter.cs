// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace SharpRpc
{
    public sealed class ObjectStreamWriter<T> : StreamWriterBase<T>
    {
        private readonly IStreamMessageFactory<T> _factory;
        private IStreamPage<T> _queue;
        private IStreamPage<T> _pageToSend;
        private readonly bool _canImmediatelyReusePages;

        internal ObjectStreamWriter(string callId, TxPipeline msgTransmitter, IStreamMessageFactory<T> factory, bool allowSending,
            StreamOptions options, IRpcLogger logger)

            : base(callId, msgTransmitter, factory, allowSending, options, logger)
        {
            _factory = factory;
            _canImmediatelyReusePages = Tx.ImmediateSerialization;

            _queue = AllocatePage();
            if (_canImmediatelyReusePages)
                _pageToSend = AllocatePage();
        }

        protected override bool DataIsAvailable => _queue.Items.Count > 0;
        protected override bool HasSpaceInQueue => _queue.Items.Count < MaxPageSize;
        public override int QueueSize => _queue.Items.Count;

        protected override void DropQueue() => _queue.Items.Clear();
        protected override void EnqueueItem(T item) => _queue.Items.Add(item);

        protected override void FillSendBuffer()
        {
            if (_canImmediatelyReusePages)
            {
                var page = _pageToSend;
                _pageToSend = _queue;
                _queue = page;
            }
            else
            {
                _queue = AllocatePage();
                _pageToSend = _queue;
            }
        }

        protected override void FreeSendBuffer(out int sentDataSize)
        {
            sentDataSize = _pageToSend.Items.Count;

            if (_canImmediatelyReusePages)
                _pageToSend.Items.Clear();
            else
                _pageToSend = null;
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
            Tx.TrySendAsync(_pageToSend, OnPageSendCompleted);
        }

        protected override ArraySegment<T> ReserveBulkWriteBuffer()
        {
            throw new NotSupportedException();
        }

        protected override void CommitBulkWriteBuffer(int writeSize)
        {
            throw new NotSupportedException();
        }

        private IStreamPage<T> AllocatePage()
        {
            var page = _factory.CreatePage(CallId);
            page.Items = new List<T>();
            return page;
        }
    }
}
