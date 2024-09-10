// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Tcp;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class SocketTransport : ByteTransport
    {
        private readonly Socket _socket;
        private readonly TaskFactory _taskFactory;
        //private readonly bool _isServer;
        
        public SocketTransport(Socket socket, TaskFactory taskQueue, string channelId, IRpcLogger logger)
            : base(channelId, logger)
        {
            _socket = socket;
            _taskFactory = taskQueue;
        }

        internal Socket Socket => _socket;

        public override void Init(Channel channel)
        {
            //_channelId = channel.Id;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return new ValueTask<int>(_socket.ReceiveAsync(buffer, SocketFlags.None));
        }

        public override ValueTask Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return new ValueTask(_socket.SendAsync(data, SocketFlags.None));
        }
#else
        public override Task<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            //return _stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count, cToken);
            return _socket.ReceiveAsync(buffer, SocketFlags.None);
        }

        public override Task Send (ArraySegment<byte> data, CancellationToken cToken)
        {
            //return _stream.WriteAsync(data.Array, data.Offset, data.Count, cToken);
            return _socket.SendAsync(data, SocketFlags.None);
        }
#endif

        public override RpcResult TranslateException(Exception ex)
        {
            return ToRpcResult(ex);
        }

        public static RpcResult ToRpcResult(Exception ex)
        {
            var socketEx = ex as SocketException ?? ex.InnerException as SocketException;

            if (socketEx != null)
            {
                switch (socketEx.SocketErrorCode)
                {
                    case SocketError.TimedOut: return new RpcResult(RpcRetCode.ConnectionTimeout, socketEx.Message);
                    case SocketError.Shutdown: return new RpcResult(RpcRetCode.ConnectionAbortedByPeer, socketEx.Message);
                    case SocketError.OperationAborted: return new RpcResult(RpcRetCode.OperationCanceled, socketEx.Message);
                    case SocketError.ConnectionAborted: return new RpcResult(RpcRetCode.OperationCanceled, ex.Message);
                    case SocketError.ConnectionReset: return new RpcResult(RpcRetCode.ConnectionAbortedByPeer, ex.Message);
                    case SocketError.HostNotFound: return new RpcResult(RpcRetCode.HostNotFound, ex.Message);
                    case SocketError.HostUnreachable: return new RpcResult(RpcRetCode.HostUnreachable, ex.Message);
                    case SocketError.ConnectionRefused: return new RpcResult(RpcRetCode.ConnectionRefused, ex.Message);
                    default: return new RpcResult(RpcRetCode.OtherConnectionError, ex.Message);
                }
            }
            else if (ex is Win32Exception w32ex && w32ex.Source == "System.Net.Security")
            {
                return new RpcResult(RpcRetCode.SecurityError, ex.Message);
            }
            else if (ex is ObjectDisposedException || ex.InnerException is ObjectDisposedException)
            {
                return new RpcResult(RpcRetCode.OperationCanceled, ex.Message);
            }

            return new RpcResult(RpcRetCode.OtherConnectionError, "An unexpected exception is occurred in TcpTransport: " + ex.Message);
        }

        protected override async Task ShutdownInternal()
        {
            try
            {
                await _socket.DisconnectAsync(_taskFactory).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Warn("Socket disconnect operation failed! " + ex.Message);
            }
        }

        protected override void DisposeInternal()
        {
            try
            {
                _socket.Dispose();
            }
            catch (Exception ex)
            {
                Warn("Socket dispose operation failed! " + ex.Message);
            }
        }

        public override TransportInfo GetInfo() => CreateInfobject(_socket);

        internal static TcpConnectionInfo CreateInfobject(Socket socket)
        {
            return new TcpConnectionInfo(socket.RemoteEndPoint as IPEndPoint, socket.LocalEndPoint as IPEndPoint);
        }
    }
}
