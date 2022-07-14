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
    public class UdsServerEndpoint : ServerEndpoint
    {
        private readonly Socket _socket;
        private readonly SocketListener _listener;
        private readonly UnixDomainSocketEndPoint _endpoint;
        private readonly TcpServerSecurity _security;

        public UdsServerEndpoint(string socketPath, TcpServerSecurity security)
        {
            if (string.IsNullOrEmpty(socketPath))
                throw new ArgumentNullException(nameof(socketPath));
            _security = security ?? throw new ArgumentNullException(nameof(security));

            if (File.Exists(socketPath))
                File.Delete(socketPath); // dotnet expects us delete file beforehand

            _endpoint = new UnixDomainSocketEndPoint(socketPath);

            _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            _listener = new SocketListener(_socket, this, _security, OnAccept, OnConnect);
        }

        protected override void Start()
        {
            LoggerAdapter.Info(Name, "listening at {0}, security: {1}", _endpoint, _security.Name);

            _listener.Start(_endpoint);
        }

        protected override Task StopAsync()
        {
            return _listener.Stop();
        }

        private void OnAccept(Socket scoket)
        {
        }
    }
}
#endif
