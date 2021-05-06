// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class RxStream<T> : IAsyncEnumerable<T>, IAsyncEnumerator<T>
    {
        private object _lockObj = new object();
        //private TaskCompletionSource<IStreamPage<T>> _pageWaitHandle;
        private readonly List<T> _items = new List<T>();

        public RxStream(IStreamFixtureFactory<T> factory)
        {

        }

        internal void OnRx(IStreamPage<T> page)
        {
            lock (_lockObj)
            {
            }
        }

        #region IAsyncEnumerable, IAsyncEnumerable

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this;
        }

        public T Current => throw new NotImplementedException();

        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

        #endregion
    }
}
