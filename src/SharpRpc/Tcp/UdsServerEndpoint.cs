// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET5_0_OR_GREATER
using SharpRpc.Tcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class UdsServerEndpoint : ServerEndpoint, ISocketListenerContext
    {
        private readonly Socket _socket;
        private readonly SocketListener _listener;
        private readonly UnixDomainSocketEndPoint _endpoint;

        public UdsServerEndpoint(string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath))
                throw new ArgumentNullException(nameof(socketPath));

            if (File.Exists(socketPath))
                File.Delete(socketPath); // dotnet expects us delete file beforehand

            _endpoint = new UnixDomainSocketEndPoint(socketPath);

            _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            _listener = new SocketListener(_socket, this, this, ServiceRegistry);
        }

        bool ISocketListenerContext.IsHostNameResolveSupported => false;

        public UdsServiceBinding BindService(ServiceDescriptor descriptor)
        {
            var binding = new UdsServiceBinding(null, descriptor);
            ServiceRegistry.Add(binding);
            return binding;
        }

        public TcpServiceBinding BindService(string serviceName, ServiceDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name is invalid!");

            var binding = new TcpServiceBinding(serviceName, descriptor);

            ServiceRegistry.Add(binding);
            return binding;
        }

        protected override void Start()
        {
            GetLogger().Info(Name, "listening at {0}", _endpoint);

            _listener.Start(_endpoint);
        }

        protected override Task StopAsync()
        {
            return _listener.Stop();
        }

        void ISocketListenerContext.OnAccept(Socket socket)
        {
        }

        void ISocketListenerContext.OnNewConnection(ServiceBinding serviceCfg, ByteTransport transport)
        {
            OnNewConnection(serviceCfg, transport);
        }
    }
}
#endif
