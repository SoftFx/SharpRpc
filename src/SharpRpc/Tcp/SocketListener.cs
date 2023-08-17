// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Tcp
{
    internal class SocketListener
    {
        private readonly string _logId;
        private readonly Socket _socket;
        private readonly ServerEndpoint _endpoint;
        private volatile bool _stopFlag;
        private Task _listenerTask;
        private readonly ISocketListenerContext _context;
        private readonly ServiceRegistry _services;

        public SocketListener(Socket socket, ServerEndpoint endpoint, ISocketListenerContext conext, ServiceRegistry services)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(_endpoint));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logId = endpoint.Name + ".Listener";
            _context = conext ?? throw new ArgumentNullException(nameof(_context));
            _services = services;
        }

        private IRpcLogger Logger => _endpoint.GetLogger();

        public void Start(EndPoint socketEndpoint)
        {
            _socket.Bind(socketEndpoint);
            _socket.Listen(100);

            _listenerTask = AcceptLoop();
        }

        public async Task Stop()
        {
            _stopFlag = true;
            _socket.Close();
            await _listenerTask;
        }

        private async Task AcceptLoop()
        {
            var handshaker = new HandshakeCoordinator(1024 * 10, TimeSpan.FromSeconds(10));

            while (!_stopFlag)
            {
                Socket socket = null;

                // ** accept **
                try
                {
                    socket = await _socket.AcceptAsync().ConfigureAwait(false);

                    _context.OnAccept(socket);
                }
                catch (Exception ex)
                {
                    var socketEx = ex as SocketException;

                    if (!_stopFlag || socketEx == null || socketEx.SocketErrorCode != SocketError.OperationAborted)
                        Logger.Error(_logId, ex.Message, null);

                    if (socket != null)
                        CloseSocket(socket);

                    continue;
                }

                if (Logger.VerboseEnabled)
                    Logger.Verbose(_logId, "Accepted new connection.");

                SocketTransport unsecuredTransport = null;

                try
                {

                    // do handshake
                    unsecuredTransport = new SocketTransport(socket, _endpoint.TaskQueue);
                    var handshakeResult = await handshaker.DoServerSideHandshake(unsecuredTransport, _services, new Log(_logId, Logger));

                    if (!handshakeResult.WasAccepted)
                    {
                        await CloseTransport(unsecuredTransport);
                        continue;
                    }

                    var serviceConfig = (TcpServiceBinding)handshakeResult.Service;

                    if (Logger.VerboseEnabled)
                        Logger.Verbose(_logId, "Handshake completed.");

                    // secure
                    var transport = await serviceConfig.Security.SecureTransport(unsecuredTransport, _endpoint);

                    // open new session
                    _context.OnNewConnection(serviceConfig, transport);
                }
                catch (Exception ex)
                {
                    Logger.Error(_logId, ex.Message);

                    if (unsecuredTransport != null)
                        await CloseTransport(unsecuredTransport);
                    else
                        CloseSocket(socket);
                }
            }
        }

        private async Task CloseTransport(ByteTransport transport)
        {
            try
            {
                await transport.Shutdown().ConfigureAwait(false);
            }
            catch { }

            try
            {
                transport.Dispose();
            }
            catch { }
        }

        private void CloseSocket(Socket socket)
        {
            try
            {
                //await socket.DisconnectAsync(_endpoint.TaskQueue);
                socket.Close();
            }
            catch { }

            try
            {
                socket.Dispose();
            }
            catch { }
        }
    }

    internal interface ISocketListenerContext
    {
        bool IsHostNameResolveSupported { get; }

        void OnAccept(Socket socket);
        void OnNewConnection(ServiceBinding serviceCfg, ByteTransport transport);
    }
}
