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
        public string Id { get; internal set; }

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
    }


    public class SessionOpenedEventArgs : EventArgs
    {
    }

    public class SessionClosedEventArgs : EventArgs
    {
    }
}
