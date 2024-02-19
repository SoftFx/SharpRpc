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
        //private TaskCompletionSource<ILogoutMessage> _logoutWaitHandle;

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

        public abstract Task<bool> OnConnect(CancellationToken cToken);
        public abstract Task OnDisconnect(CancellationToken abortToken);

        protected abstract RpcResult OnLoginMessage(ILoginMessage loginMsg);
        protected abstract RpcResult OnLogoutMessage(ILogoutMessage logoutMsg);
        protected abstract RpcResult OnLogoutRequestMessage(ILogoutRequestMessage logoutRequestMsg);
        protected virtual void OnInit() { }

        //protected abstract Task RiseClosingEvent(bool isFaulted);

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

        //public async Task OnDisconnect(CancellationToken abortToken)
        //{
        //    lock (LockObj)
        //    {
        //        State = SessionState.CloseEvent;
        //        _logoutWaitHandle = new TaskCompletionSource<ILogoutMessage>();
        //    }

        //    await RiseClosingEvent(abortToken.IsCancellationRequested);

        //    if (!abortToken.IsCancellationRequested)
        //    {
        //        using (abortToken.Register(AbortLogoutWait))
        //        {
        //            lock (LockObj)
        //                State = SessionState.PendingLogout;

        //            Channel.Tx.StopProcessingUserMessages(Channel.Fault);

        //            var sendResult = await SendLogout();

        //            if (sendResult.IsOk)
        //            {
        //                await _logoutWaitHandle.Task;
        //                await Task.Yield(); // exit the lock
        //            }
        //            else
        //                Channel.Logger.Warn(Channel.Id, "Failed to send a logout message! " + sendResult.FaultMessage);
        //        }
        //    }

        //    lock (LockObj)
        //        State = SessionState.LoggedOut;
        //}

        //private void AbortLogoutWait()
        //{
        //    lock (LockObj)
        //    {
        //        if (!_logoutWaitHandle.Task.IsCompleted)
        //            _logoutWaitHandle.SetResult(null);
        //    }
        //}

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

        //private RpcResult OnLogoutMessage(ILogoutMessage logoutMsg)
        //{
        //    lock (LockObj)
        //    {
        //        if (State == SessionState.CloseEvent || State == SessionState.PendingLogout)
        //        {
        //            _logoutWaitHandle.SetResult(logoutMsg);
        //            return RpcResult.Ok;
        //        }
        //        else if (State != SessionState.LoggedIn)
        //            return new RpcResult(RpcRetCode.UnexpectedMessage, $"Received an unexpected logout message! State='{State}'.");

        //        Channel.TriggerDisconnect(new RpcResult(RpcRetCode.ChannelClosedByOtherSide, "Logout requested by other side."));
        //        return RpcResult.Ok;
        //    }   
        //}
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
