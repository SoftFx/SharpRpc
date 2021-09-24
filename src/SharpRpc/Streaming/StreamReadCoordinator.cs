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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class StreamReadCoordinator
    {
        private readonly object _lockObj;
        private readonly string _callId;
        private readonly IStreamMessageFactory _factory;
        private ushort _itemsConsumed;
        private bool _isSending;

        public StreamReadCoordinator(object lockObj, string callId, IStreamMessageFactory factory)
        {
            _lockObj = lockObj;
            _callId = callId;
            _factory = factory;
        }

        public IStreamPageAck OnPageConsume(int pageSize)
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            _itemsConsumed += (ushort)pageSize;

            if (!_isSending)
            {
                _isSending = true;
                return CreateAck();
            }

            return null;
        }

        public IStreamPageAck OnAckSent()
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            if (_itemsConsumed > 0)
                return CreateAck();

            //Debug.WriteLine("RC done sending");

            _isSending = false;
            return null;
        }

        private IStreamPageAck CreateAck()
        {
            //Debug.WriteLine("RC sending ack...");

            var ack = _factory.CreatePageAcknowledgement(_callId);
            ack.Consumed = _itemsConsumed;
            _itemsConsumed = 0;
            return ack;
        }
    }
}
