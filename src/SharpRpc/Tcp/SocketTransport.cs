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
        private readonly NetworkStream _stream;
        private readonly TaskFactory _taskQueue;
        private string _channelId;
        
        public SocketTransport(Socket socket, TaskFactory taskQueue)
        {
            _socket = socket;
            _taskQueue = taskQueue;
            _stream = new NetworkStream(socket, false);
        }

#if NET5_0_OR_GREATER
        public override bool StopRxByShutdown => false;
#else
        public override bool StopRxByShutdown => true;
#endif

        public override void Init(Channel channel)
        {
            _channelId = channel.Id;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return _stream.ReadAsync(buffer, cToken);
        }

        public override ValueTask Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _stream.WriteAsync(data, cToken);
        }
#else
        public override Task<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return _stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count);
        }

        public override Task Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _stream.WriteAsync(data.Array, data.Offset, data.Count);
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
                    case SocketError.Shutdown: return new RpcResult(RpcRetCode.ConnectionShutdown, socketEx.Message);
                    case SocketError.OperationAborted: return new RpcResult(RpcRetCode.ConnectionShutdown, socketEx.Message);
                    case SocketError.ConnectionAborted: return new RpcResult(RpcRetCode.ConnectionShutdown, ex.Message);
                    case SocketError.ConnectionReset: return new RpcResult(RpcRetCode.ConnectionAbortedByPeer, ex.Message);
                    default: return new RpcResult(RpcRetCode.OtherConnectionError, ex.Message);
                }
            }
            else if (ex is Win32Exception w32ex && w32ex.Source == "System.Net.Security")
            {
                return new RpcResult(RpcRetCode.SecurityError, ex.Message);
            }

            return new RpcResult(RpcRetCode.OtherConnectionError, "An unexpected exception is occurred in TcpTransport: " + ex.Message);
        }

        public override async Task Shutdown()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
                // TO DO : log
            }

            try
            {
                await _socket.DisconnectAsync(_taskQueue);
            }
            catch (Exception)
            {
                // TO DO : log
            }

            _socket.Close();
        }

        public override void Dispose()
        {
            _stream.Dispose();
            _socket.Dispose();
        }

        public override TransportInfo GetInfo() => CreateInfobject(_socket);

        internal static TcpConnectionInfo CreateInfobject(Socket socket)
        {
            return new TcpConnectionInfo(socket.RemoteEndPoint as IPEndPoint, socket.LocalEndPoint as IPEndPoint);
        }
    }
}
