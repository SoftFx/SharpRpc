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
using System.Threading.Tasks.Dataflow;

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
        private ActionBlock<Socket> _sessionInitBlock;

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
            _sessionInitBlock = new ActionBlock<Socket>(InitSession, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 10, BoundedCapacity = 10 });

            _socket.Bind(socketEndpoint);
            _socket.Listen(100);

            _listenerTask = AcceptLoop();
        }

        public async Task Stop()
        {
            _stopFlag = true;
            _socket.Close();
            await _listenerTask.ConfigureAwait(false);

            _sessionInitBlock.Complete();
            await _sessionInitBlock.Completion.ConfigureAwait(false);
        }

        private async Task AcceptLoop()
        {
            while (!_stopFlag)
            {
                Socket socket = null;

                // ** accept **
                try
                {
                    socket = await _socket.AcceptAsync().ConfigureAwait(false);
                    _context.OnAccept(socket);

                    if (Logger.IsVerboseEnabled)
                        Logger.Verbose(_logId, "Accepted new connection.");

                    if (!await _sessionInitBlock.SendAsync(socket).ConfigureAwait(false))
                        throw new Exception("Assertion failed! AcceptLoop() must be stopped before stopping the block!");
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
            }
        }

        private async Task InitSession(Socket socket)
        {
            var handshaker = new HandshakeCoordinator(1024 * 10, TimeSpan.FromSeconds(10));

            SocketTransport unsecuredTransport = null;

            try
            {
                // do handshake
                unsecuredTransport = new SocketTransport(socket, _endpoint.TaskFactory);
                var handshakeResult = await handshaker.DoServerSideHandshake(unsecuredTransport, _services, new Log(_logId, Logger)).ConfigureAwait(false);

                if (!handshakeResult.WasAccepted)
                {
                    await CloseTransport(unsecuredTransport).ConfigureAwait(false);
                    return;
                }

                var serviceConfig = (TcpServiceBinding)handshakeResult.Service;

                if (Logger.IsVerboseEnabled)
                    Logger.Verbose(_logId, "Handshake completed.");

                // secure
                var transport = await serviceConfig.Security.SecureTransport(unsecuredTransport, _endpoint).ConfigureAwait(false);

                // open new session
                _context.OnNewConnection(serviceConfig, transport);
            }
            catch (Exception ex)
            {
                Logger.Error(_logId, ex.Message);

                if (unsecuredTransport != null)
                    await CloseTransport(unsecuredTransport).ConfigureAwait(false);
                else
                    CloseSocket(socket);
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
                //await socket.DisconnectAsync(_endpoint.TaskQueue).ConfigureAwait(false);
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
