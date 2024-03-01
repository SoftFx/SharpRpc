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
    public abstract class ClientEndpoint : Endpoint
    {
        private Credentials _creds = Credentials.None;
        private IRpcLogger _logger = NullLogger.Instance;

        public abstract Task<RpcResult<ByteTransport>> ConnectAsync(CancellationToken cToken);

        public Credentials Credentials
        {
            get => _creds;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _creds = value;
                }
            }
        }

        internal override IRpcLogger GetLogger() => _logger;

        public IRpcLogger Logger
        {
            get => _logger;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _logger = value ?? NullLogger.Instance;
                }
            }
        }
    }
}
