﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ServerEndpoint : Endpoint
    {
        //private ServerCredentials _creds = ServerCredentials.None;
        private Authenticator _authenticator = Authenticator.None;
        private RpcServer _serverObj;

        public ServerEndpoint()
        {
        }

        internal override IRpcLogger GetLogger() => _serverObj.Logger;

        public Authenticator Authenticator
        {
            get => _authenticator;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _authenticator = value ?? throw new ArgumentNullException(nameof(value));
                }
            }
        }

        internal void Init(RpcServer server)
        {
            LockTo(server);
            _serverObj = server;
        }

        protected abstract void Start();
        protected abstract Task StopAsync();

        protected void OnConnect(ByteTransport newConnection)
        {
            if (GetLogger().VerboseEnabled)
                GetLogger().Verbose(Name, "Incoming connection");

            ClientConnected.Invoke(this, newConnection);
        }

        internal event Action<ServerEndpoint, ByteTransport> ClientConnected;

        internal void InvokeStart()
        {
            //Logger.Verbose(Name, "Starting...");

            Start();

            //Logger.Verbose(Name, "Started.");
        }

        internal async Task InvokeStop()
        {
            //Logger.Verbose(Name, "Stopping...");

            try
            {
                await StopAsync();
            }
            catch (Exception ex)
            {
                GetLogger().Error(Name, ex, "Stop failed! " + ex.Message);
            }

            //Logger.Verbose(Name, "Stopped.");
        }
    }
}
