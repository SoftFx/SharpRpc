// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Tcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class SslTransport : ByteTransport
    {
        private readonly SslStream _stream;
        private readonly Socket _socket;
        private IRpcLogger _logger;
        private string _channelId;

        public SslTransport(SslStream stream, Socket socket)
        {
            _stream = stream;
            _socket = socket;
        }

        public override void Init(Channel channel)
        {
            _logger = channel.Logger;
            _channelId = channel.Id;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return _stream.ReadAsync(buffer);
        }

        public override ValueTask Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _stream.WriteAsync(data);
        }
#else
        protected override Task<int> ReceiveInternal(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return _stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count);
        }

        protected override Task SendInternal(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _stream.WriteAsync(data.Array, data.Offset, data.Count, cToken);
        }
#endif

        public override RpcResult TranslateException(Exception ex)
        {
            return SocketTransport.ToRpcResult(ex);
        }

#if NET5_0_OR_GREATER
        public override async Task Shutdown()
#else
        protected override async Task ShutdownInternal()
#endif
        {
            try
            {
#if NETSTANDARD
               _stream.Close();
#else
                await _stream.ShutdownAsync().ConfigureAwait(false);
#endif
            }
            catch (Exception ex)
            {
                _logger.Error(GetName(), "Shutdown() failed: " + ex.Message);
            }
        }

#if NET5_0_OR_GREATER
        public override void Dispose()
#else
        protected override void DisposeInternal()
#endif
        {
            _stream.Dispose();
        }

        public override TransportInfo GetInfo()
        {
            return SocketTransport.CreateInfobject(_socket);
        }

        private string GetName()
        {
            return $"{_channelId}-SslTransport";
        }
    }
}
