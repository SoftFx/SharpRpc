// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ByteTransport
    {
#if NET5_0_OR_GREATER
        public abstract ValueTask Send(ArraySegment<byte> data, CancellationToken cToken);
        public abstract ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken);
#else
        public abstract Task Send(ArraySegment<byte> data, CancellationToken cToken);
        public abstract Task<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken);
#endif
        public abstract RpcResult TranslateException(Exception ex);

        public abstract void Init(Channel channel);

        public abstract Task Shutdown();
        public abstract void Dispose();

        public abstract ITransportInfo GetInfo();
    }

    public interface ITransportInfo
    {
    }
}
