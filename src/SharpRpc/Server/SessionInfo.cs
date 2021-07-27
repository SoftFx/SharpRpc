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
    public class SessionInfo
    {
        private Channel _ch;

        internal void Init(Channel channel)
        {
            _ch = channel;
            Id = _ch.Id;
        }

        public string Id { get; private set; }

        public event EventHandler<SessionOpenedEventArgs> Opened;
        public event EventHandler<SessionClosedEventArgs> Closed;

        internal void FireOpened(SessionOpenedEventArgs args)
        {
            Opened?.Invoke(this, args);
        }

        internal void FireClosed(SessionClosedEventArgs args)
        {
            Closed?.Invoke(this, args);
        }

#if PF_COUNTERS
        public int RxMessagePageCount => _ch.GetRxMessagePageCount();
        public double AverageRxChunkSize => _ch.GetAverageRxChunkSize();
        public double AverageRxMessagePageSize => _ch.GetAverageRxMessagePageSize();
#endif
    }


    public class SessionOpenedEventArgs : EventArgs
    {
    }

    public class SessionClosedEventArgs : EventArgs
    {
    }
}
