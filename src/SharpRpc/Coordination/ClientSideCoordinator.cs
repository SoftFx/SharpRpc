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
        private object _lockObj = new object();
        private States _state = States.PendingLogin;
        private TaskCompletionSource<ILoginMessage> _loginWaitHandle;
        private TaskCompletionSource<ILogoutMessage> _logoutWaitHandle;
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
            Channel.Dispatcher.Start();
        }


#if NET5_0_OR_GREATER
        public override async ValueTask<RpcResult> OnConnect(CancellationToken cToken)
#else
        public override async Task<RpcResult> OnConnect(CancellationToken cToken)
#endif
        {
            lock (_lockObj)
            {
                _state = States.LoginInProgress;
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
                    return Channel.Dispatcher.OnSessionEstablished();
                }
                else if (loginResp.ResultCode == LoginResult.InvalidCredentials)
                    return new RpcResult(RpcRetCode.InvalidCredentials, "Login failed: " + loginResp.ErrorMessage);
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Login failed: Invalid or missing result code in login response!");
            }
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<RpcResult> OnDisconnect(LogoutOption option)
#else
        public override async Task<RpcResult> OnDisconnect(LogoutOption option)
#endif
        {
            lock (_lockObj)
            {
                // the session has been already closed
                if (_state == States.LoggedOut)
                    return RpcResult.Ok;

                if (option == LogoutOption.Immidiate)
                    _state = States.LoggedOut;
                else
                {
                    _state = States.LogoutInProgress;
                    _logoutWaitHandle = new TaskCompletionSource<ILogoutMessage>();
                }
            }

            var logoutMsg = Channel.Contract.SystemMessages.CreateLogoutMessage();
            //logoutMsg.Mode = option;

            var sendResult = await Channel.Tx.SendSystemMessage(logoutMsg);

            lock (_lockObj)
            {
                if (!sendResult.IsOk)
                    return sendResult;

                if (option == LogoutOption.Immidiate)
                {
                    _state = States.LoggedOut;
                    return RpcResult.Ok;
                }
            }

            // TO DO: add wait timeout
            await _logoutWaitHandle.Task;

            lock (_lockObj)
                _state = States.LoggedOut;

            return RpcResult.Ok;
        }

        public override RpcResult OnMessage(ISystemMessage message)
        {
            lock (_lockObj)
            {
                if (message is ILoginMessage loginMsg)
                {
                    if (_state != States.LoginInProgress)
                        return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                    _state = States.LoggedIn;
                    _loginWaitHandle.TrySetResult(loginMsg);
                }
                else if (message is ILogoutMessage logoutMsg)
                {
                    if (_state == States.LoginInProgress)
                        _logoutWaitHandle.TrySetResult(logoutMsg);
                    else
                    {
                        _state = States.LoggedOut;
                        return new RpcResult(RpcRetCode.LogoutRequest, "Connection is closed by server side.");
                    }
                }
                
            }

            return RpcResult.Ok;
        }

        private void OnLoginTimeout()
        {
            _loginWaitHandle.TrySetResult(null);
        }
    }
}
