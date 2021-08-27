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
    internal class StreamReadCoordinator
    {
        private readonly string _callId;
        private readonly IStreamMessageFactory _factory;
        private ushort _pagesConsumedCount;
        private bool _isSending;

        public StreamReadCoordinator(string callId, IStreamMessageFactory factory)
        {
            _callId = callId;
            _factory = factory;
        }

        public IStreamPageAck OnPageConsume()
        {
            _pagesConsumedCount++;

            if (!_isSending)
            {
                _isSending = true;
                return CreateAck();
            }

            return null;
        }

        public IStreamPageAck OnAckSent()
        {
            if (_pagesConsumedCount > 0)
                return CreateAck();

            _isSending = false;
            return null;
        }

        private IStreamPageAck CreateAck()
        {
            var ack = _factory.CreatePageAcknowledgement(_callId);
            ack.PagesConsumed = _pagesConsumedCount;
            _pagesConsumedCount = 0;
            return ack;
        }
    }
}
