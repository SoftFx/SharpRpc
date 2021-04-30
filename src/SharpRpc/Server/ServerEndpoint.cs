// Copyright © 2021 Soft-Fx. All rights reserved.
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
        private RpcServer _server;
        //private ServerCredentials _creds = ServerCredentials.None;
        private Authenticator _authenticator = Authenticator.None;

        public ServerEndpoint()
        {
            Name = Namer.GetInstanceName(GetType());
        }

        public string Name { get; }

        //public ServerCredentials Credentials
        //{
        //    get => _creds;
        //    set
        //    {
        //        lock (_stateLockObj)
        //        {
        //            ThrowIfImmutable();
        //            _creds = value ?? throw new ArgumentNullException(nameof(value));
        //        }
        //    }
        //}

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
            lock (_stateLockObj)
            {
                if (_server != null)
                    throw new InvalidOperationException("This endpoint belongs to other server!");

                _server = server;
            }
        }

        protected abstract void Start(LoggerFacade logger);
        protected abstract Task StopAsync(LoggerFacade logger);

        protected void OnConnect(ByteTransport newConnection)
        {
            _server.Logger.Verbose(Name, "Incoming connection");

            ClientConnected.Invoke(this, newConnection);
        }

        internal event Action<ServerEndpoint, ByteTransport> ClientConnected;

        internal void InvokeStart()
        {
            _server.Logger.Info(Name, "Starting...");

            Start(_server.Logger);

            _server.Logger.Info(Name, "Started.");
        }

        internal async Task InvokeStop()
        {
            _server.Logger.Info(Name, "Stopping...");

            try
            {
                await StopAsync(_server.Logger);
            }
            catch (Exception ex)
            {
                _server.Logger.Error(Name, ex, "Stop failed! " + ex.Message);
            }

            _server.Logger.Info(Name, "Stopping...");
        }
    }
}
