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
using System.Threading.Tasks;

namespace SharpRpc
{
    public class RpcServer
    {
        private enum ServerState { Idle, Starting, Online, Stopping  }

        private readonly object _stateLock = new object();
        private readonly List<ServerEndpoint> _endpoints = new List<ServerEndpoint>();
        private ServerState _state;
        private readonly Dictionary<string, Channel> _sessions = new Dictionary<string, Channel>();
        private readonly ServiceBinding _binding;

        public RpcServer(ServiceBinding binding)
        {
            _binding = binding ?? throw new ArgumentNullException("binding");

            Name = Namer.GetInstanceName(GetType());
        }

        public RpcServer AddEndpoint(ServerEndpoint endpoint)
        {
            lock (_stateLock)
            {
                endpoint.Init(this);
                endpoint.ClientConnected += Endpoint_ClientConnected;
                _endpoints.Add(endpoint);
            }

            return this;
        }

        internal LoggerFacade Logger { get; } = new LoggerFacade();

        public string Name { get; }

        public RpcServer SetLogger(IRpcLogger logger)
        {
            lock (_stateLock)
            {
                ThrowIfConfigProhibited();
                Logger.SetExtLogger(logger);
            }

            return this;
        }

        public void Start()
        {
            lock (_stateLock)
            {
                if (_state != ServerState.Idle)
                    throw new InvalidOperationException("Start is not possible at this time! State: " + _state);

                _state = ServerState.Starting;
            }

            Logger.Info(Name, "Starting...");

            try
            {
                foreach (var endpoint in _endpoints)
                    endpoint.Validate();

                foreach (var endpoint in _endpoints)
                    endpoint.InvokeStart();

                Logger.Info(Name, "Started.");

                lock (_stateLock)
                    _state = ServerState.Online;
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                    _state = ServerState.Idle;

                // TO DO: stop started endpoints
                throw new Exception("Failed to start RPC server! " + ex.Message);
            }
        }

        public async Task StopAsync()
        {
            lock (_stateLock)
            {
                if (_state != ServerState.Online)
                    throw new InvalidOperationException("Stop is not possible at this time! State: " + _state);

                _state = ServerState.Stopping;
            }

            Logger.Info(Name, "Stopping...");

            await StopEndpoints();
            await CloseAllSessions();

            Logger.Info(Name, "Stopped.");

            lock (_stateLock)
                _state = ServerState.Idle;
        }

        private async Task StopEndpoints()
        {
            var stopTasks = _endpoints.Select(e => e.InvokeStop());
            await Task.WhenAll(stopTasks.ToList());
        }

        private async Task CloseAllSessions()
        {
            var toClose = new List<Channel>();

            lock (_stateLock)
            {
                toClose.AddRange(_sessions.Values);
                _sessions.Clear();
            }

            var closeTasks = toClose
                .AsParallel()
                .Select(c =>
                    {
                        c.Closed -= Session_Closed;
                        return c.CloseAsync();
                    })
                .ToList();

            await Task.WhenAll(closeTasks);
        }

        private void Endpoint_ClientConnected(ServerEndpoint sender, ByteTransport transport)
        {
            bool abortConnection = false;

            lock (_stateLock)
            {
                if (_state == ServerState.Online || _state == ServerState.Starting)
                {
                    var serviceImpl = _binding.CreateServiceImpl();
                    var session = new Channel(true, sender, _binding.Descriptor, serviceImpl);
                    session.Closed += Session_Closed;
                    session.Init(transport);
                    _sessions.Add(session.Id, session);
                    Logger.Verbose(Name, "New session: " + session.Id);
                }
                else
                    abortConnection = true;
            }

            if (abortConnection)
            {
                transport.Shutdown();
                Logger.Verbose(Name, "Incoming connection was aborted!");
            }
        }

        private void Session_Closed(Channel channel, RpcResult fault)
        {
            lock (_sessions)
            {
                _sessions.Remove(channel.Id);
            }

            if (!IsFaultClose(fault.Code))
                Logger.Verbose(Name, "Session " + channel.Id + " was closed.");
            else
                Logger.Verbose(Name, "Session " + channel.Id + " was faulted. Code: " + fault.Code + " Message: " + fault.FaultMessage);
        }

        private void ThrowIfConfigProhibited()
        {
            if (_state != ServerState.Idle)
                throw new InvalidOperationException("Changing configuration in runtime is prohibited!");
        }

        private bool IsFaultClose(RpcRetCode code)
        {
            return code != RpcRetCode.Ok && code != RpcRetCode.ChannelClosed && code != RpcRetCode.LogoutRequest;
        }
    }
}
