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
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpClientEndpoint : ClientEndpoint
    {
        //private readonly string _address;
        private readonly string _serviceName;
        //private readonly int _port;
        private readonly TcpSecurity _security;
        private readonly DnsEndPoint _endpoint;

        public TcpClientEndpoint(string urlString, TcpSecurity security)
        {
            var url = new Uri(urlString);
            _serviceName = url.Fragment;
            _endpoint = new DnsEndPoint(url.DnsSafeHost, url.Port);
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public TcpClientEndpoint(string hostName, int port, TcpSecurity security)
            : this(hostName, null, port, security)
        {
        }

        public TcpClientEndpoint(string hostName, string serviceName, int port, TcpSecurity security)
        {
            _endpoint = new DnsEndPoint(hostName, port);
            _security = security ?? throw new ArgumentNullException(nameof(security));
            _serviceName = serviceName;
        }

        public TcpClientEndpoint(DnsEndPoint dnsEndpoint, TcpSecurity security)
        {
            _endpoint = dnsEndpoint ?? throw new ArgumentNullException(nameof(dnsEndpoint));
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public TcpClientEndpoint(DnsEndPoint dnsEndpoint, string serviceName, TcpSecurity security)
        {
            _endpoint = dnsEndpoint ?? throw new ArgumentNullException(nameof(dnsEndpoint));
            _security = security ?? throw new ArgumentNullException(nameof(security));
            _serviceName = serviceName;
        }

        public override async Task<RpcResult<ByteTransport>> ConnectAsync(CancellationToken cancelToken, string channelId)
        {
            //IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(_endpoint.Host).ConfigureAwait(false);
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            //var targetEndpoint = new IPEndPoint(ipAddress, _endpoint.Port);

            Socket socket = null;
            SocketTransport unsecuredTransport = null;
            try
            {
                // Create a TCP/IP socket.  
                socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                // connect
#if NET5_0_OR_GREATER
                await socket.ConnectAsync(_endpoint, cancelToken).ConfigureAwait(false);
                {
#else
                using (cancelToken.Register(() => socket.Close()))
                {
                    await socket.ConnectAsync(_endpoint).ConfigureAwait(false);
#endif

                    // handshake
                    using var handshaker = new HandshakeCoordinator(HandshakeTimeout, cancelToken);
                    unsecuredTransport = new SocketTransport(socket, TaskFactory, channelId, Logger);
                    var hsResult = await handshaker.DoClientSideHandshake(unsecuredTransport, _endpoint.Host, _serviceName).ConfigureAwait(false);

                    if (!hsResult.IsOk)
                    {
                        await CloseTransport(unsecuredTransport).ConfigureAwait(false);
                        return hsResult;
                    }

                    // secure
                    var secureTransport = await _security.SecureTransport(socket, this, _endpoint.Host, channelId, Logger, handshaker.CancelToken).ConfigureAwait(false);
                    if (secureTransport != unsecuredTransport)
                    {
                        // Release resources used by leftover transport
                        unsecuredTransport.DetachSocket();
                        await CloseTransport(unsecuredTransport).ConfigureAwait(false);
                        unsecuredTransport = null;
                    }

                    return new RpcResult<ByteTransport>(secureTransport);
                }
            }
            catch (Exception ex)
            {
                if (unsecuredTransport != null)
                    await CloseTransport(unsecuredTransport).ConfigureAwait(false);
                else
                    CloseSocket(socket);

                var fault = SocketTransport.ToRpcResult(ex);
                return new RpcResult<ByteTransport>(fault.Code, fault.FaultMessage);
            }
        }
    }
}
