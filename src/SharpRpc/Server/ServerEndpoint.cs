// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ServerEndpoint : Endpoint
    {
        private RpcServer _serverObj;

        public ServerEndpoint()
        {
            ServiceRegistry = new ServiceRegistry(this);
        }

        internal override IRpcLogger GetLogger() => _serverObj.Logger;
        internal ServiceRegistry ServiceRegistry { get; }

        protected abstract void Start();
        protected abstract Task StopAsync();

        internal void OnNewConnection(ServiceBinding sConfig, ByteTransport newConnection)
        {
            ClientConnected.Invoke(this, sConfig, newConnection);
        }

        internal event Action<ServerEndpoint, ServiceBinding, ByteTransport> ClientConnected;

        protected override void ValidateAndInitialize()
        {
            base.ValidateAndInitialize();

            ServiceRegistry.BuildCache();
        }

        protected override void OnAttached()
        {
            _serverObj = (RpcServer)Parent;
        }

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
                await StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GetLogger().Error(Name, ex, "Stop failed! " + ex.Message);
            }

            //Logger.Verbose(Name, "Stopped.");
        }
    }
}
