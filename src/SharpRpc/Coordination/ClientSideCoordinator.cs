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
    internal class ClientSideCoordinator : SessionCoordinator
    {
        private TaskCompletionSource<ILoginMessage> _loginWaitHandle;
        //private TaskCompletionSource<ILogoutMessage> _logoutWaitHandle;
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

        public override async Task<RpcResult> OnConnect(CancellationToken cToken)
        {
            lock (LockObj)
            {
                State = SessionState.PendingLogin;
                _loginWaitHandle = new TaskCompletionSource<ILoginMessage>();
            }

            // send login
            var loginMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            _creds.OnBeforeLogin(loginMsg);

            var sendResult = await Channel.Tx.SendSystemMessage(loginMsg);

            if (!sendResult.IsOk)
                return sendResult;

            using (cToken.Register(OnLoginTimeout))
            {
                // wait for response login (with timeout)
                var loginResp = await _loginWaitHandle.Task;

                if (loginResp == null)
                    return new RpcResult(RpcRetCode.LoginTimeout, "Login oepration timed out!");

                if (loginResp.ResultCode == LoginResult.Ok)
                {
                    // enable message queue
                    return Channel.Dispatcher.Start();
                }
                else if (loginResp.ResultCode == LoginResult.InvalidCredentials)
                    return new RpcResult(RpcRetCode.InvalidCredentials, "Login failed: " + loginResp.ErrorMessage);
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Login failed: Invalid or missing result code in login response!");
            }
        }

        protected override RpcResult OnLoginMessage(ILoginMessage loginMsg)
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogin)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                State = SessionState.OpeningEvent;
                _loginWaitHandle.TrySetResult(loginMsg);
                return RpcResult.Ok;
            }
        }

        protected override Task RiseClosingEvent(bool isFaulted)
        {
            return Channel.RiseClosingEvent(isFaulted);
        }

        private void OnLoginTimeout()
        {
            _loginWaitHandle.TrySetResult(null);
        }
    }
}
