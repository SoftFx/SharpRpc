﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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

        public TcpClientEndpoint(string address, int port, TcpSecurity security)
        {
            _address = address;
            _port = port;
            _security = security ?? throw new ArgumentNullException("security");
        }

        public override async Task<RpcResult<ByteTransport>> ConnectAsync()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(_address);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, _port);

            try
            {
                // Create a TCP/IP socket.  
                var socket = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                await socket.ConnectAsync(remoteEP);

                return new RpcResult<ByteTransport>(await _security.SecureTransport(socket, this, _address));
            }
            catch (Exception ex)
            {
                var fault = TcpTransport.ToRpcResult(ex);
                return new RpcResult<ByteTransport>(fault.Code, fault.FaultMessage);
            }
        }
    }
}
