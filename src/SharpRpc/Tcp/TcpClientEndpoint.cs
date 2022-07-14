// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpClientEndpoint : ClientEndpoint
    {
        private readonly string _address;
        private readonly int _port;
        private readonly TcpSecurity _security;
        private readonly IPEndPoint _endpoint;

        public TcpClientEndpoint(string address, int port, TcpSecurity security)
        {
            _address = address ?? throw new ArgumentNullException(nameof(address));
            _port = port;
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public TcpClientEndpoint(IPEndPoint ipEndpoint, TcpSecurity security)
        {
            _endpoint = ipEndpoint ?? throw new ArgumentNullException(nameof(ipEndpoint));
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public override async Task<RpcResult<ByteTransport>> ConnectAsync()
        {
            IPEndPoint targetEndpoint;

            if (_endpoint == null)
            {
                IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(_address);
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                targetEndpoint = new IPEndPoint(ipAddress, _port);
            }
            else
                targetEndpoint = _endpoint;

            try
            {
                // Create a TCP/IP socket.  
                var socket = new Socket(targetEndpoint.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                await socket.ConnectAsync(targetEndpoint);

                return new RpcResult<ByteTransport>(await _security.SecureTransport(socket, this, _address));
            }
            catch (Exception ex)
            {
                var fault = SocketTransport.ToRpcResult(ex);
                return new RpcResult<ByteTransport>(fault.Code, fault.FaultMessage);
            }
        }
    }
}
