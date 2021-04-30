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
    internal class ServerSideCoordinator : SessionCoordinator
    {
        private object _lockObj = new object();
        private States _state = States.PendingLogin;
        private Authenticator _authPlugin;
        private readonly TaskCompletionSource<ILoginMessage> _loginWaitHandle = new TaskCompletionSource<ILoginMessage>();
        //private readonly CancellationTokenSource _loginWaitCancel = new CancellationTokenSource();

        public override TimeSpan LoginTimeout => TimeSpan.FromSeconds(5);

        protected override void OnInit()
        {
            var serverEndpoint = (ServerEndpoint)Channel.Endpoint;
            _authPlugin = serverEndpoint.Authenticator;
        }

        public override async ValueTask<RpcResult> OnConnect(CancellationToken cToken)
        {
            ILoginMessage loginMsg;

            // wait for login (with timeout)
            try
            {
                using (cToken.Register(OnLoginTimeout))
                    loginMsg = await _loginWaitHandle.Task;
            }
            catch (TaskCanceledException)
            {
                return new RpcResult(RpcRetCode.LoginTimeout, "");
            }

            // enable message queue
            Channel.Dispatcher.AllowMessages();

            // exit lock
            await Task.Yield();

            // check login/password
            var authError = await _authPlugin.OnLogin(loginMsg);

            // send login response
            var loginRespMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            loginRespMsg.ResultCode = authError == null ? LoginResult.Ok : LoginResult.InvalidCredentials;
            loginRespMsg.ErrorMessage = authError;
            await Channel.Tx.SendSystemMessage(loginRespMsg);

            if (authError == null)
            {
                // start processing messages
                Channel.Dispatcher.Start();

                return RpcResult.Ok;
            }
            else
                return new RpcResult(RpcRetCode.InvalidCredentials, authError);
        }

        public override ValueTask<RpcResult> OnDisconnect()
        {
            return ValueTask.FromResult(RpcResult.Ok);
        }

        public override RpcResult OnMessage(ISystemMessage message)
        {
            lock (_lockObj)
            {
                if (message is ILoginMessage loginMsg)
                {
                    if (_state != States.PendingLogin)
                        return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                    _state = States.LoginInProgress;

                    _loginWaitHandle.TrySetResult(loginMsg);
                }
                else if (message is ILogoutMessage logoutMsg)
                {
                }
            }

            return RpcResult.Ok;
        }

        private void OnLoginTimeout()
        {
            _loginWaitHandle.TrySetCanceled();
        }
    }
}
