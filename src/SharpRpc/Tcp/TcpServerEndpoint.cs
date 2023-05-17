// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using SharpRpc.Tcp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpServerEndpoint : ServerEndpoint, ISocketListenerContext
    {
        private readonly Socket _socket;
        private readonly SocketListener _listener;
        private readonly IPEndPoint _ipEndpoint;
        private bool _ipv6Only = true;

        public const int PickUnusedPort = 0;

        public TcpServerEndpoint(IPEndPoint ipEndpoint)
        {
            _ipEndpoint = ipEndpoint ?? throw new ArgumentNullException("security");
            _socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener = new SocketListener(_socket, this, this, ServiceRegistry);
        }

        public TcpServerEndpoint(IPAddress address, int port)
            : this(new IPEndPoint(address, port))
        {
        }

        public TcpServerEndpoint(string address)
        {
            var addressParts = address.Split(':');

            if (addressParts.Length != 2)
                throw new ArgumentException("Invalid address format. Please provide address in host:port format.");

            if (!int.TryParse(addressParts[1].Trim(), out int port) || port < 0)
                throw new ArgumentException("Invalid port. Port must be a positive integer.");

            IPHostEntry ipHostInfo = Dns.GetHostEntry(addressParts[0].Trim());
            var ipAddress = ipHostInfo.AddressList[0];
            _ipEndpoint = new IPEndPoint(ipAddress, port);

            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener = new SocketListener(_socket, this, this, ServiceRegistry);
        }

        public TcpServiceBinding BindService(ServiceDescriptor descriptor)
        {
            var binding = new TcpServiceBinding(null, descriptor);
            ServiceRegistry.Add(binding);
            return binding;
        }

        public TcpServiceBinding BindService(string serviceName, ServiceDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name is invalid!");

            var binding = new TcpServiceBinding(serviceName, descriptor);

            ServiceRegistry.Add(serviceName, binding);
            return binding;
        }

        /// <summary>
        /// Setting this option to false enables socket to listen at both IPv6 and IPv4 protocols simultaneously.
        /// This option only works if endpoint is initially configured for IPv6 communications.
        /// Default value: true.
        /// </summary>
        public bool IPv6Only
        {
            get => _ipv6Only;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _ipv6Only = value;
                }
            }
        }

        public int EffectivePort { get; private set; }

        bool ISocketListenerContext.IsHostNameResolveSupported => throw new NotImplementedException();

        protected override void Start()
        {
            GetLogger().Info(Name, "listening at {0}", _ipEndpoint);

            _socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, _ipv6Only);

            _listener.Start(_ipEndpoint);

            EffectivePort = ((IPEndPoint)_socket.LocalEndPoint).Port;
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

#if NET5_0_OR_GREATER
        protected virtual ValueTask<ByteTransport> GetTransport(Socket socket)
        {
            return new ValueTask<ByteTransport>(new SocketTransport(socket, TaskQueue));
        }
#else
        protected virtual Task<ByteTransport> GetTransport(Socket socket)
        {
            return Task.FromResult<ByteTransport>(new SocketTransport(socket, TaskQueue));
        }
#endif
    }
}
