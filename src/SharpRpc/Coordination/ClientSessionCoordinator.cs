﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SharpRpc.Lib.SlimAwaitable;

namespace SharpRpc
{
    internal class ClientSessionCoordinator : SessionCoordinator
    {
        private TaskCompletionSource<bool> _connectWaitHandle;
        private Credentials _creds;

#if DEBUG
        public override TimeSpan LoginTimeout => TimeSpan.FromMinutes(2);
#else
        public override TimeSpan LoginTimeout => TimeSpan.FromSeconds(5);
#endif

        protected override void OnInit()
        {
            var clientEndpoint = (ClientEndpoint)Channel.Endpoint;
            _creds = clientEndpoint.Credentials;
        }

        public override Task<bool> OnConnect(CancellationToken cToken)
        {
            lock (LockObj)
            {
                State = SessionState.PendingLogin;
                _connectWaitHandle = new TaskCompletionSource<bool>();
            }

            cToken.Register(OnLoginTimeout);

            // send login
            var loginMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            _creds.OnBeforeLogin(loginMsg);
            Channel.Tx.TrySendSystemMessage(loginMsg, OnLoginSendCompleted);

            return _connectWaitHandle.Task;
        }

        protected override RpcResult OnLoginMessage(ILoginMessage loginMsg)
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogin)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                if (loginMsg.ResultCode == LoginResult.Ok)
                {
                    State = SessionState.OpenEvent;
                    // enable message queue
                    Channel.Tx.StartProcessingUserMessages();
                    Channel.Dispatcher.Start();
                }
                else
                {
                    State = SessionState.LoginFailed;
                    Channel.UpdateFault(new RpcResult(RpcRetCode.InvalidCredentials, "Login failed: " + loginMsg.ErrorMessage));
                }
            }

            Channel.RiseOpeningEvent()
                .ContinueWith(OnOpenEventCompleted);

            return RpcResult.Ok;
        }

        private void OnLoginSendCompleted(RpcResult result)
        {
            if (!result.IsOk)
            {
                lock (LockObj)
                {
                    State = SessionState.LoginFailed;
                    Channel.UpdateFault(result);
                }

                _connectWaitHandle.SetResult(false);
            }
        }

        private void OnOpenEventCompleted(Task<bool> openEventTask)
        {
            bool loggedIn;

            lock (LockObj)
            {
                if (openEventTask.Result)
                {
                    State = SessionState.OpenEvent;
                    loggedIn = true;
                }
                else
                {
                    State = SessionState.LoginFailed;
                    Channel.UpdateFault(new RpcResult(RpcRetCode.ChannelOpenEventFailed, "An error occurred in the channel open event handler!"));
                    loggedIn = false;
                }
            }

            _connectWaitHandle.SetResult(loggedIn);
        }

        protected override Task RiseClosingEvent(bool isFaulted)
        {
            return Channel.RiseClosingEvent(isFaulted);
        }

        private void OnLoginTimeout()
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogin)
                    return;

                State = SessionState.LoginFailed;
                Channel.UpdateFault(new RpcResult(RpcRetCode.LoginTimeout, "Login oepration timed out!"));
            }

            _connectWaitHandle.TrySetResult(false);
        }
    }
}
