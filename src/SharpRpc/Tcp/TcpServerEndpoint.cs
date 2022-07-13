﻿// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Tcp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpServerEndpoint : ServerEndpoint
    {
        private readonly Socket _socket;
        private readonly SocketListener _listener;
        private readonly IPEndPoint _ipEndpoint;
        private readonly TcpServerSecurity _security;
        private bool _ipv6Only = true;

        public const int PickUnusedPort = 0;

        public TcpServerEndpoint(IPEndPoint ipEndpoint, TcpServerSecurity security)
        {
            _security = security ?? throw new ArgumentNullException("security");
            _ipEndpoint = ipEndpoint ?? throw new ArgumentNullException("security");
            _socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener = new SocketListener(_socket, this, security, OnConnect);
        }

        public TcpServerEndpoint(IPAddress address, int port, TcpServerSecurity security)
            : this(new IPEndPoint(address, port), security)
        {
        }

        public TcpServerEndpoint(string address, TcpServerSecurity security)
        {
            var addressParts = address.Split(':');

            if (addressParts.Length != 2)
                throw new ArgumentException("Invalid address format. Please provide address in host:port format.");

            if (!int.TryParse(addressParts[1].Trim(), out int port) || port < 0)
                throw new ArgumentException("Invalid port. Port must be a positive integer.");

            _security = security ?? throw new ArgumentNullException("security");

            IPHostEntry ipHostInfo = Dns.GetHostEntry(addressParts[0].Trim());
            var ipAddress = ipHostInfo.AddressList[0];
            _ipEndpoint = new IPEndPoint(ipAddress, port);

            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener = new SocketListener(_socket, this, _security, OnConnect);
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
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _ipv6Only = value;
                }
            }
        }

        public int EffectivePort { get; private set; }

        protected override void Start()
        {
            LoggerAdapter.Info(Name, "listening at {0}, security: {1}", _ipEndpoint, _security.Name);

            _socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, _ipv6Only);

            _listener.Start(_ipEndpoint);

            EffectivePort = ((IPEndPoint)_socket.LocalEndPoint).Port;
        }

        protected override Task StopAsync()
        {
            return _listener.Stop();
        }

#if NET5_0_OR_GREATER
        protected virtual ValueTask<ByteTransport> GetTransport(Socket socket)
        {
            return new ValueTask<ByteTransport>(new TcpTransport(socket, TaskQueue));
        }
#else
        protected virtual Task<ByteTransport> GetTransport(Socket socket)
        {
            return Task.FromResult<ByteTransport>(new TcpTransport(socket, TaskQueue));
        }
#endif
    }
}
