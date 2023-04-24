// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using SharpRpc.Server;
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
        //private States _state = States.PendingLogin;
        private Authenticator _authPlugin;
        private readonly TaskCompletionSource<ILoginMessage> _loginWaitHandle = new TaskCompletionSource<ILoginMessage>();
        //private readonly CancellationTokenSource _loginWaitCancel = new CancellationTokenSource();
        private TaskFactory _taskQueue;
        private readonly SessionContext _sharedContextObj;

        public ServerSideCoordinator(SessionContext sharedContext)
        {
            _sharedContextObj = sharedContext;
        }

#if DEBUG
        public override TimeSpan LoginTimeout => TimeSpan.FromMinutes(2);
#else
        public override TimeSpan LoginTimeout => TimeSpan.FromSeconds(5);
#endif

        protected override void OnInit()
        {
            var serverEndpoint = (ServerEndpoint)Channel.Endpoint;
            _authPlugin = serverEndpoint.Authenticator;
            _taskQueue = serverEndpoint.TaskQueue;
        }

        public override async Task<RpcResult> OnConnect(CancellationToken cToken)
        {
            ILoginMessage loginMsg;

            lock (LockObj)
                State = SessionState.PendingLogin;

            Channel.Logger.Verbose(Channel.Id, "Waiting for login message...");

            // wait for login (with timeout)
            try
            {
                using (cToken.Register(OnLoginCanceled))
                    loginMsg = await _loginWaitHandle.Task;
            }
            catch (TaskCanceledException)
            {
                return new RpcResult(RpcRetCode.LoginTimeout, "");
            }

            Channel.Logger.Verbose(Channel.Id, "Login message has been received. Checking credentials...");

            // exit lock
            await _taskQueue.Dive();

            // check login/password
            var authError = await _authPlugin.OnLogin(loginMsg, _sharedContextObj);

            Channel.Logger.Verbose(Channel.Id, "Sending login responce...");

            // send login response
            var loginRespMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            loginRespMsg.ResultCode = authError == null ? LoginResult.Ok : LoginResult.InvalidCredentials;
            loginRespMsg.ErrorMessage = authError;
            await Channel.Tx.SendSystemMessage(loginRespMsg);

            if (authError == null)
            {
                lock (LockObj)
                    State = SessionState.LoggedIn;

                // start processing messages
                var startResult = Channel.Dispatcher.Start();

                if (startResult.IsOk)
                {
                    Channel.Logger.Verbose(Channel.Id, "Succesful login.");
                    return RpcResult.Ok;
                }
                else
                    return startResult;
            }
            else
                return new RpcResult(RpcRetCode.InvalidCredentials, authError);
        }

        protected override RpcResult OnLoginMessage(ILoginMessage loginMsg)
        {
            lock (LockObj)
            {
                if (State != SessionState.PendingLogin)
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                //State = SessionState.OpeningEvent;

                _loginWaitHandle.TrySetResult(loginMsg);
                return RpcResult.Ok;
            }
        }

        protected override Task RiseClosingEvent(bool isFaulted)
        {
            return Task.FromResult(true);
        }

        private void OnLoginCanceled()
        {
            _loginWaitHandle.TrySetCanceled();
        }
    }
}
