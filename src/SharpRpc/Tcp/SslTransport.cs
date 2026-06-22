// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Tcp;
using System;
using System.Collections.Generic;
using System.IO;
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

        public SslTransport(SslStream stream, Socket socket, string channelId, IRpcLogger logger)
            : base(channelId, logger)
        {
            _stream = stream;
            _socket = socket;
        }

        public override void Init(Channel channel)
        {
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
            return _stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count, cToken);
        }

        public override Task Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _stream.WriteAsync(data.Array, data.Offset, data.Count, cToken);
        }
#endif

        public override RpcResult TranslateException(Exception ex)
        {
            return SocketTransport.ToRpcResult(ex);
        }

#if NETSTANDARD
        protected override Task ShutdownInternal()
        {
            // netstandard2.0 SslStream has no ShutdownAsync. The previous implementation called
            // _stream.Close() here, but Shutdown() runs before the rx loop is stopped, so closing
            // (disposing) the SslStream while a ReadAsync is still in flight triggers the same
            // AccessViolationException described in DisposeInternal(). There is no graceful TLS
            // close_notify available on this target anyway, so defer all teardown to DisposeInternal(),
            // which disposes the socket first and is therefore safe with a read in flight.
            return Task.CompletedTask;
        }
#else
        protected override async Task ShutdownInternal()
        {
            try
            {
                await _stream.ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Warn("ShutdownInternal() failed: " + ex.Message);
            }
        }
#endif

        protected override void DisposeInternal()
        {
            // The underlying socket MUST be torn down before the SslStream.
            //
            // SslStream.ReadAsync does not abort the underlying socket read when its cancellation token
            // fires: cancelling the rx token unblocks the awaiting RxLoop (OperationCanceledException) and
            // the rx pipeline reports itself closed, but the native receive that SslStream issued into its
            // internal (pooled) buffer is still pending in the OS. Disposing the SslStream at that point
            // frees that buffer / native security state while the kernel still owns a pointer to it, so when
            // the read later completes on an IOCP thread it writes into freed memory -> AccessViolationException.
            //
            // Disposing the socket first aborts the pending receive at the OS level (it completes with
            // OperationAborted and copies no data); only then is it safe to dispose the SslStream.
            // Socket.Dispose() is idempotent, so the SslStream disposing its owned NetworkStream/socket
            // afterwards is harmless.
            try
            {
                _socket.Dispose();
            }
            catch (Exception ex)
            {
                Warn("DisposeInternal() failed to dispose the socket: " + ex.Message);
            }

            try
            {
                _stream.Dispose();
            }
            catch (Exception ex)
            {
                Warn("DisposeInternal() failed: " + ex.Message);
            }
        }

        public override TransportInfo GetInfo()
        {
            return SocketTransport.CreateInfobject(_socket);
        }
    }
}
