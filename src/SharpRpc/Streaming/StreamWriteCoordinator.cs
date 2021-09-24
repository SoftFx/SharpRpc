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
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class StreamWriteCoordinator
    {
        private readonly object _lockObj;
        private readonly int _windowSize;
        private int _windowFill;

        public StreamWriteCoordinator(object lockObj, int windowSize)
        {
            _lockObj = lockObj;
            _windowSize = windowSize;
        }

        public bool IsBlocked { get; private set; }

        public void OnPageSent(int pageSize)
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            _windowFill += pageSize;
            if (_windowFill >= _windowSize)
                IsBlocked = true;
        }

        public void OnAcknowledgementRx(IStreamPageAck ack)
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            _windowFill -= ack.Consumed;

            // TO DO : check if _windowFill less than zero

            if (_windowFill < _windowSize)
                IsBlocked = false;
        }
    }
}
