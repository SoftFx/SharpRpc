// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET5_0_OR_GREATER
using SharpRpc.Lib;
using System;
using System.Net.Sockets;
using System.Threading;
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

        public override async Task<RpcResult<ByteTransport>> ConnectAsync(CancellationToken cToken, string channelId)
        {
            Socket socket = null;
            try
            { 
                socket = new Socket(_endpoint.AddressFamily,
                    SocketType.Stream, ProtocolType.IP);

                await socket.ConnectAsync(_endpoint, cToken).ConfigureAwait(false);

                using var timeoutSrc = new TimeoutCancelTokenSrc(HandshakeTimeout, cToken);
                var secureTransport = await _security.SecureTransport(socket, this, "localhost", channelId, Logger, timeoutSrc.Token).ConfigureAwait(false);

                return new RpcResult<ByteTransport>(secureTransport);
            }
            catch (Exception ex)
            {
                CloseSocket(socket);
                var fault = SocketTransport.ToRpcResult(ex);
                return new RpcResult<ByteTransport>(fault.Code, fault.FaultMessage);
            }
        }   
    }
}
#endif
