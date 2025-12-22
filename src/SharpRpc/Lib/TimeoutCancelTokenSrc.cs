// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;

namespace SharpRpc.Lib
{
    internal class TimeoutCancelTokenSrc : IDisposable
    {
        private readonly CancellationTokenSource _cancelSrc;
        private readonly CancellationTokenRegistration _externalTokenSub;

        public CancellationToken Token => _cancelSrc.Token;

        public TimeoutCancelTokenSrc(TimeSpan timeout, CancellationToken externalToken)
        {
            _cancelSrc = new CancellationTokenSource(timeout);
            _externalTokenSub = externalToken.Register(OnExternalCancel);
        }

        public void Dispose()
        {
            _cancelSrc.Dispose();
            _externalTokenSub.Dispose();
        }

        private void OnExternalCancel()
        {
            _cancelSrc.Cancel();
        }
    }
}
