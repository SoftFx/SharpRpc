// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

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

        public SslTransport(SslStream stream)
        {
            _stream = stream;
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

        public override async Task Shutdown()
        {
            try
            {
                await _stream.ShutdownAsync();
            }
            catch (Exception)
            {
                // TO DO: log
            }
        }

        public override void Dispose()
        {
            _stream.Dispose();
        }
    }
}
