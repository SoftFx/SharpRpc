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
        public abstract Task Shutdown();
        public abstract void Dispose();
#else
        private bool _disposed = false;
        private readonly object _disposedSync = new object();

        public Task Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            Task result = null;
            lock (_disposedSync)
            {
                if (_disposed || cToken.IsCancellationRequested)
                    result = Task.CompletedTask;
                else
                    result = SendInternal(data, cToken);
            }
            return result;
        }

        public Task<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            Task<int> result = null;
            lock (_disposedSync)
            {
                if (_disposed || cToken.IsCancellationRequested)
                    result = Task.FromResult(0);
                else
                    result = ReceiveInternal(buffer, cToken);
            }
            return result;
        }

        public Task Shutdown()
        {
            lock (_disposedSync)
                _disposed = true;

            return ShutdownInternal();
        }

        public void Dispose()
        {
            lock (_disposedSync)
                _disposed = true;

            DisposeInternal();
        }

        protected abstract Task SendInternal(ArraySegment<byte> data, CancellationToken cToken);
        protected abstract Task<int> ReceiveInternal(ArraySegment<byte> buffer, CancellationToken cToken);
        protected abstract Task ShutdownInternal();
        protected abstract void DisposeInternal();
#endif
        public abstract RpcResult TranslateException(Exception ex);

        public abstract void Init(Channel channel);

        public abstract TransportInfo GetInfo();
    }

    public abstract class TransportInfo
    {
        internal abstract void DumptTo(Log log);
    }
}
