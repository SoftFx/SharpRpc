// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

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
        private readonly Action<ByteTransport> _onConnect;
        private readonly Action<Socket> _configureSocket;
        private readonly TcpServerSecurity _security;

        public SocketListener(Socket socket, ServerEndpoint endpoint, TcpServerSecurity security,
            Action<Socket> socketConfigAction, Action<ByteTransport> onConnect)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(_endpoint));
            _security = security ?? throw new ArgumentNullException(nameof(security));
            _logId = endpoint.Name + ".Listener";
            
            _configureSocket = socketConfigAction ?? throw new ArgumentNullException(nameof(socketConfigAction));
            _onConnect = onConnect ?? throw new ArgumentNullException(nameof(onConnect));
        }

        private IRpcLogger Logger => _endpoint.GetLogger();

        public void Start(EndPoint socketEndpoint)
        {
            _security.Init();

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

                    _configureSocket(socket);
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

                try
                {

                    // do handshake
                    var unsecuredTransport = new SocketTransport(socket, _endpoint.TaskQueue);
                    var handshakeResult = await handshaker.DoServerSideHandshake(unsecuredTransport);

                    if (!handshakeResult.IsOk)
                    {
                        Logger.Info(_logId, "Handshake failed: " + handshakeResult.FaultMessage);

                        try
                        {
                            await socket.DisconnectAsync(_endpoint.TaskQueue);
                        }
                        catch (Exception)
                        {
                        }

                        CloseSocket(socket);

                        continue;
                    }

                    Logger.Verbose(_logId, "Completed a handshake.");

                    // secure
                    var transport = await _security.SecureTransport(socket, _endpoint);

                    // open new session
                    _onConnect(transport);
                }
                catch (Exception ex)
                {
                    Logger.Error(_logId, ex.Message);
                    CloseSocket(socket);
                }
            }
        }

        private void CloseSocket(Socket socket)
        {
            try
            {
                //await socket.DisconnectAsync(_endpoint.TaskQueue);
                socket.Close();
                socket.Dispose();
            }
            catch
            {
            }
        }
    }
}
