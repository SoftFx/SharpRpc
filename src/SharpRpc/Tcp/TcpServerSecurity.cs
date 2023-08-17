// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class TcpServerSecurity
    {
        public static TcpServerSecurity None { get; } = new NullServerSecurity();

        internal abstract string Name { get; }
        internal abstract void Init();

#if NET5_0_OR_GREATER
        internal abstract ValueTask<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint);
#else
        internal abstract Task<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint);
#endif

        private class NullServerSecurity : TcpServerSecurity
        {
            internal override void Init()
            {
            }

            internal override string Name => "None";

#if NET5_0_OR_GREATER
            internal override ValueTask<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint)
            {
                return new ValueTask<ByteTransport>(unsecureTransport);
            }
#else
            internal override Task<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint)
            {
                return Task.FromResult<ByteTransport>(unsecureTransport);
            }
#endif
        }
    }
}
