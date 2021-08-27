// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class StreamWriteCoordinator
    {
        private readonly int _windowSize;
        private int _windowFill;

        public StreamWriteCoordinator(int windowSize)
        {
            _windowSize = windowSize;
        }

        public bool IsBlocked { get; private set; }

        public void OnPageSent()
        {
            _windowFill++;
            if (_windowFill >= _windowSize)
                IsBlocked = true;
        }

        public void OnAcknowledgementRx(IStreamPageAck ack)
        {
            _windowFill -= ack.PagesConsumed;

            // TO DO : check if _windowFill less than zero

            if (_windowFill < _windowSize)
                IsBlocked = false;
        }
    }
}
