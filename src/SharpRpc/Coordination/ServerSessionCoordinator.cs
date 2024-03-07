// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class ServerSessionCoordinator : SessionCoordinator
    {
        private Authenticator _authPlugin;
        private readonly SessionContext _sharedContextObj;
        private TaskCompletionSource<bool> _connectWaitHandle;
        private TaskCompletionSource<bool> _disconnectWaitHandle;
        private bool _isLogoutReceived;

        public ServerSessionCoordinator(SessionContext sharedContext)
        {
            _sharedContextObj = sharedContext;
        }

        protected override void OnInit()
        {
            //var serverEndpoint = (ServerEndpoint)Channel.Endpoint;
            _authPlugin = Channel.Binding.Authenticator;
            //_taskQueue = serverEndpoint.TaskQueue;
        }

        public override Task<bool> OnConnect(CancellationToken timeoutToken)
        {
            State = SessionState.PendingLogin; // no lock is required at this time
            Channel.Logger.Verbose(Channel.Id, "Waiting for login message...");
            _connectWaitHandle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            timeoutToken.Register(OnLoginTimeout);
            return _connectWaitHandle.Task;
        }

        protected override RpcResult OnLoginMessage(ILoginMessage loginMsg)
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogin)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                State = SessionState.Authentication;
            }

            Channel.Logger.Verbose(Channel.Id, "Login message has been received. Checking credentials...");

            var authResult = _authPlugin.OnLogin(loginMsg, _sharedContextObj);

#if NET5_0_OR_GREATER
            if (!authResult.IsCompleted)
                authResult.AsTask().ContinueWith(OnAuthResult);
            else
                OnAuthResult(authResult.Result);
#else
            authResult.ContinueWith(OnAuthResult);
#endif

            return RpcResult.Ok;
        }

        private void OnLoginTimeout()
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogin)
                    return;

                State = SessionState.LoginFailed;
                IsCoordinationBroken = true;
                Channel.UpdateFault(new RpcResult(RpcRetCode.LoginTimeout, "Timeout has been reached while waiting for the login message!"));
            }

            Channel.Logger.Warn(Channel.Id, "Timeout has been reached while waiting for the login message!");
            _connectWaitHandle.SetResult(false);
        }

        private void OnAuthResult(Task<string> authTask)
        {
            OnAuthResult(authTask.Result);
        }

        private void OnAuthResult(string authError)
        {
            string loginError;

            lock (LockObj)
            {
                if (authError == null)
                {
                    // start processing messages
                    Channel.Tx.StartProcessingUserMessages();
                    var startResult = Channel.Dispatcher.Start();

                    if (!startResult.IsOk)
                    {
                        State = SessionState.LoginFailed;
                        Channel.UpdateFault(startResult);
                        loginError = startResult.FaultMessage;
                    }
                    else
                        State = SessionState.LoggedIn;
                }
                else
                {
                    State = SessionState.LoginFailed;
                    Channel.UpdateFault(new RpcResult(RpcRetCode.InvalidCredentials, authError));
                }
            }

            SendLoginResponse(authError);
            _connectWaitHandle.SetResult(authError == null);
        }

        private void SendLoginResponse(string authError)
        {
            // send login response
            var loginRespMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            loginRespMsg.ResultCode = authError == null ? LoginResult.Ok : LoginResult.InvalidCredentials;
            loginRespMsg.ErrorMessage = authError;

            Channel.Tx.TrySendSystemMessage(loginRespMsg, OnLoginSendCompleted);
        }

        private void OnLoginSendCompleted(RpcResult result)
        {
            // TO DO
            if (!result.IsOk)
            {

            }
        }

        protected override RpcResult OnLogoutMessage(ILogoutMessage logoutMsg)
        {
            bool isDisconnecting = false;

            lock (LockObj)
            {
                _isLogoutReceived = true;

                if (State == SessionState.PendingLogout)
                {
                    isDisconnecting = true;
                    State = SessionState.CloseEvent;
                }
            }

            if (isDisconnecting)
                RiseClosingEvent(false);
            else
                Channel.TriggerDisconnect(new RpcResult(RpcRetCode.ChannelClosedByOtherSide, "Logout requested by other side."));

            return RpcResult.Ok;
        }

        protected override RpcResult OnLogoutRequestMessage(ILogoutRequestMessage logoutRequestMsg)
        {
            return new RpcResult(RpcRetCode.UnexpectedMessage, $"Received an unexpected logout request message! Logout requests are not supported by the server side!'.");
        }

        public override Task OnDisconnect()
        {
            bool isLogoutRequestRequired;

            lock (LockObj)
            {
                if (State != SessionState.LoggedIn)
                    return Task.CompletedTask;

                if (_isLogoutReceived || IsCoordinationBroken)
                {
                    State = SessionState.CloseEvent;
                    isLogoutRequestRequired = false;
                }
                else
                {
                    State = SessionState.PendingLogout;
                    isLogoutRequestRequired = true;
                }

                _disconnectWaitHandle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            if (isLogoutRequestRequired)
                SendLogoutRequest(OnLogoutRequestSent);
            else
                RiseClosingEvent(IsCoordinationBroken);

            return _disconnectWaitHandle.Task;
        }

        public override void AbortCoordination()
        {
            IsCoordinationBroken = true;

            if (State == SessionState.PendingLogin)
                _connectWaitHandle.TrySetResult(false);
            else if (State == SessionState.PendingLogout)
                _disconnectWaitHandle.TrySetResult(false);
        }

        private void RiseClosingEvent(bool isLostConnection)
        {
            Channel.RiseClosingEvent(isLostConnection)
                .ContinueWith(OnClosingEventCompleted);
        }

        private void OnClosingEventCompleted(Task eventTask)
        {
            lock (LockObj)
            {
                State = SessionState.LoggedOut;

                if (IsCoordinationBroken)
                {
                    _disconnectWaitHandle.TrySetResult(false);
                    return;
                }
            }

            SendLogout(OnLogoutSent);
        }

        private void OnLogoutRequestSent(RpcResult result)
        {
            if (!result.IsOk)
            {
            }
        }

        private void OnLogoutSent(RpcResult result)
        {
            lock (LockObj)
                State = SessionState.LoggedOut;

            _disconnectWaitHandle.TrySetResult(result.IsOk);
        }

        private void ForceLogout()
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogout)
                    return;

                State = SessionState.LoggedOut;
            }

            _disconnectWaitHandle.TrySetResult(false);
        }

        //protected override Task RiseClosingEvent(bool isFaulted)
        //{
        //    // TO DO
        //    return Task.FromResult(true);
        //}
    }
}