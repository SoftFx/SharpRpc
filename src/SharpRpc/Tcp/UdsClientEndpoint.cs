// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET5_0_OR_GREATER
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class UdsClientEndpoint : ClientEndpoint
    {
        private readonly UnixDomainSocketEndPoint _endpoint;
        private readonly TcpSecurity _security;

        public UdsClientEndpoint(string socketPath, TcpSecurity security)
        {
            if (string.IsNullOrEmpty(socketPath))
                throw new ArgumentNullException(nameof(socketPath));
            _endpoint = new UnixDomainSocketEndPoint(socketPath);
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public override async Task<RpcResult<ByteTransport>> ConnectAsync()
        {
            try
            { 
                var socket = new Socket(_endpoint.AddressFamily,
                    SocketType.Stream, ProtocolType.IP);

                await socket.ConnectAsync(_endpoint);

                return new RpcResult<ByteTransport>(await _security.SecureTransport(socket, this, "localhost"));
            }
            catch (Exception ex)
            {
                var fault = SocketTransport.ToRpcResult(ex);
                return new RpcResult<ByteTransport>(fault.Code, fault.FaultMessage);
            }
        }
    }
}
#endif
