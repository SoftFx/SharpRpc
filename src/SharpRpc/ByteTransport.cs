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
        private readonly object _disposedSync = new object();
        private TransportCloseState _closeState = TransportCloseState.Online;
        private Task _shutdownTask;

        private readonly string _name;
        private readonly IRpcLogger _logger;

        public ByteTransport(string parentLogId, IRpcLogger logger)
        {
            _name = parentLogId + ".transport";
            _logger = logger;
        }
#if NET5_0_OR_GREATER
        public abstract ValueTask Send(ArraySegment<byte> data, CancellationToken cToken);
        public abstract ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken);
#else

        public abstract Task Send(ArraySegment<byte> data, CancellationToken cToken);
        public abstract Task<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken);
#endif
        public abstract RpcResult TranslateException(Exception ex);

        public abstract void Init(Channel channel);

        public abstract TransportInfo GetInfo();

        protected void Warn(string message)
        {
            _logger.Warn(_name, message);
        }


        public async Task Shutdown()
        {
            lock (_disposedSync)
            {
                if (_closeState != TransportCloseState.Online)
                    return;
                _closeState = TransportCloseState.Shutdown;
                _shutdownTask = ShutdownInternal();
            }

            await _shutdownTask.ConfigureAwait(false);
        }

        public async Task DisposeAsync()
        {
            Task taskToWait;
            lock (_disposedSync)
            {
                if (_closeState == TransportCloseState.Dispose)
                    return;
                _closeState = TransportCloseState.Dispose;
                taskToWait = _shutdownTask ?? Task.CompletedTask;
            }

            await taskToWait.ConfigureAwait(false);
            DisposeInternal();
        }

        protected abstract Task ShutdownInternal();
        protected abstract void DisposeInternal();

        protected enum TransportCloseState : byte
        {
            Online,
            Shutdown,
            Dispose
        }
    }

    public abstract class TransportInfo
    {
        internal abstract void DumptTo(Log log);
    }
}
