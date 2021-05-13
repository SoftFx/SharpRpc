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
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract class SessionCoordinator
    {
        protected Channel Channel { get; private set; }

        public void Init(Channel ch)
        {
            Channel = ch;
            OnInit();
        }

        public abstract TimeSpan LoginTimeout { get; }

        public abstract RpcResult OnMessage(ISystemMessage message);
        public abstract ValueTask<RpcResult> OnConnect(CancellationToken cToken);
        public abstract ValueTask<RpcResult> OnDisconnect(LogoutOption option);

        protected virtual void OnInit() { }

        public enum States
        {
            PendingLogin,
            LoginInProgress,
            LoggedIn,
            LogoutInProgress,
            LoggedOut
        }
    }
}
