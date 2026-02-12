// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Tcp;
using System;
using System.ComponentModel;
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
        private readonly SocketAsyncMethods _asyncMethods;
        //private readonly bool _isServer;

        private bool _socketDetached;

        public SocketTransport(Socket socket, TaskFactory taskQueue, string channelId, IRpcLogger logger)
            : base(channelId, logger)
        {
            _socket = socket;
            _taskFactory = taskQueue;
            _asyncMethods = new SocketAsyncMethods(false);
        }

        internal Socket Socket => _socket;

        internal void DetachSocket() => _socketDetached = true;

        public override void Init(Channel channel)
        {
            //_channelId = channel.Id;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return _socket.ReceiveAsync(buffer, SocketFlags.None, cToken);
        }

        public override ValueTask Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _socket.SendAsync(data, SocketFlags.None, cToken).AsValueTask();
        }
#else
        public override Task<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            // ReceiveAsync on .Net Framework has 2 problems:
            // 1. It keeps pinned user buffers between calls in cached SocketAsyncEventArgs.
            // 2. Cached SocketAsyncEventArgs are never disposed.
            //return _socket.ReceiveAsync(buffer, SocketFlags.None).WithCancelToken(cToken);
            return _asyncMethods.ReceiveAsync(_socket, buffer, SocketFlags.None, cToken);

            //return SocketAsyncMethods.ReceiveAsyncApm(_socket, buffer, SocketFlags.None, cToken);
        }

        public override Task Send (ArraySegment<byte> data, CancellationToken cToken)
        {
            // SendAsync on .Net Framework has 2 problems:
            // 1. It keeps pinned user buffers between calls in cached SocketAsyncEventArgs.
            // 2. Cached SocketAsyncEventArgs are never disposed.
            //return _socket.SendAsync(data, SocketFlags.None).WithCancelToken(cToken);
            return _asyncMethods.SendAsync(_socket, data, SocketFlags.None, cToken);

            //return SocketAsyncMethods.SendAsyncApm(_socket, data, SocketFlags.None, cToken);
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
                if (!_socketDetached)
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
                if (!_socketDetached)
                    _socket.Dispose();
                _asyncMethods.Dispose();
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
