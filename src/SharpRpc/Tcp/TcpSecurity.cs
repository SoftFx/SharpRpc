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
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class TcpSecurity
    {
        public static TcpSecurity None { get; } = new NullSecurity();

#if NET5_0_OR_GREATER
        internal abstract ValueTask<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger);
#else
        internal abstract Task<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger);
#endif
        private class NullSecurity : TcpSecurity
        {
#if NET5_0_OR_GREATER
            internal override ValueTask<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger)
            {
                return new ValueTask<ByteTransport>(new SocketTransport(socket, endpoint.TaskFactory, channelId, logger));
            }
#else
            internal override Task<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger)
            {
                return Task.FromResult<ByteTransport>(new SocketTransport(socket, endpoint.TaskFactory, channelId, logger));
            }
#endif
        }
    }
}
