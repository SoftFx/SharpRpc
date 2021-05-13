// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpServerEndpoint : ServerEndpoint
    {
        //private readonly object _lockObj;
        private readonly Socket _listener;
        private Task _listenerTask;
        private volatile bool _stopFlag;
        private readonly IPEndPoint _ipEndpoint;
        private readonly TcpServerSecurity _security;
        private bool _ipv6Only = true;

        public TcpServerEndpoint(IPEndPoint ipEndpoint, TcpServerSecurity security)
        {
            _security = security ?? throw new ArgumentNullException("security");
            _ipEndpoint = ipEndpoint;

            _listener = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
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

            _listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
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

        protected override void Start()
        {
            Logger.Info(Name, "listening at {0}, security: {1}", _ipEndpoint, _security.Name);

            _stopFlag = false;
            _security.Init();

            _listener.Bind(_ipEndpoint);
            _listener.Listen(100);

            _listenerTask = AcceptLoop();
        }

        protected override async Task StopAsync()
        {
            _stopFlag = true;

            _listener.Close();

            await _listenerTask;
        }

        protected virtual ValueTask<ByteTransport> GetTransport(Socket socket)
        {
            return new ValueTask<ByteTransport>(new TcpTransport(socket));
        }

        private async Task AcceptLoop()
        {
            while (!_stopFlag)
            {
                Socket socket;

                try
                {
                    socket = await _listener.AcceptAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var socketEx = ex as SocketException;

                    if (!_stopFlag || socketEx == null || socketEx.SocketErrorCode != SocketError.OperationAborted)
                        Logger.Error(Name, ex.Message);

                    continue;
                }

                try
                {
                    var transport = await _security.SecureTransport(socket);

                    OnConnect(transport);
                }
                catch (Exception ex)
                {
                    //var socketEx = ex as SocketException;
                    Logger.Error(Name, ex.Message);

                    try
                    {
                        await socket.DisconnectAsync();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
