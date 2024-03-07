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
        protected object LockObj { get; private set; }
        protected Channel Channel { get; private set; }
        protected ILogoutMessage LogoutMessage { get; private set; }
        
        public void Init(Channel ch)
        {
            Channel = ch;
            LockObj = Channel.StateLockObject;
            OnInit();
        }

        public SessionState State { get; protected set; }
        public bool IsCoordinationBroken { get; protected set; }

        public abstract Task<bool> OnConnect(CancellationToken timeoutToken);
        public abstract Task OnDisconnect();
        public abstract void AbortCoordination();

        protected abstract RpcResult OnLoginMessage(ILoginMessage loginMsg);
        protected abstract RpcResult OnLogoutMessage(ILogoutMessage logoutMsg);
        protected abstract RpcResult OnLogoutRequestMessage(ILogoutRequestMessage logoutRequestMsg);
        protected virtual void OnInit() { }

        public RpcResult OnMessage(ISystemMessage message)
        {
            if (message is ILoginMessage loginMsg)
                return OnLoginMessage(loginMsg);
            else if (message is ILogoutMessage logoutMsg)
                return OnLogoutMessage(logoutMsg);
            else if (message is ILogoutRequestMessage logoutRequestMsg)
                return OnLogoutRequestMessage(logoutRequestMsg);

            return RpcResult.Ok; // TO DO : report protocol violation
        }

        protected Task<RpcResult> SendLogout()
        {
            var logoutMsg = Channel.Contract.SystemMessages.CreateLogoutMessage();
            return Channel.Tx.SendSystemMessage(logoutMsg).ToTask();
        }

        protected void SendLogout(Action<RpcResult> onSendCompleted)
        {
            var logoutMsg = Channel.Contract.SystemMessages.CreateLogoutMessage();
            Channel.Tx.TrySendSystemMessage(logoutMsg, onSendCompleted);
        }

        protected Task<RpcResult> SendLogoutRequest()
        {
            var logoutRequetsMsg = Channel.Contract.SystemMessages.CreateLogoutRequestMessage();
            return Channel.Tx.SendSystemMessage(logoutRequetsMsg).ToTask();
        }

        protected void SendLogoutRequest(Action<RpcResult> onSendCompleted)
        {
            var logoutRequetsMsg = Channel.Contract.SystemMessages.CreateLogoutRequestMessage();
            Channel.Tx.TrySendSystemMessage(logoutRequetsMsg, onSendCompleted);
        }
    }

    public enum SessionState : byte
    {
        None,
        PendingLogin,
        Authentication, //the authentication is beign called
        LoginFailed,
        OpenEvent,
        LoggedIn,
        CloseEvent,
        PendingLogout,
        LoggedOut,
    }
}
