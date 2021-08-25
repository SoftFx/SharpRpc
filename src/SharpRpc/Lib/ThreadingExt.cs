// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    public static class ThreadingExt
    {
        public static DiveAwaitable Dive(this TaskFactory factory)
        {
            return new DiveAwaitable(factory);
        }

        public struct DiveAwaitable
        {
            private readonly TaskFactory _factory;

            public DiveAwaitable(TaskFactory factory)
            {
                _factory = factory;
            }

            public DiveAwaiter GetAwaiter() { return new DiveAwaiter(_factory); }

            public struct DiveAwaiter : ICriticalNotifyCompletion
            {
                private readonly TaskFactory _factory;

                public DiveAwaiter(TaskFactory factory)
                {
                    _factory = factory;
                }

                public bool IsCompleted { get { return false; } } // yielding is always required for YieldAwaiter, hence false

                public void OnCompleted(Action continuation)
                {
                    QueueContinuation(continuation, flowContext: true);
                }

                public void UnsafeOnCompleted(Action continuation)
                {
                    QueueContinuation(continuation, flowContext: false);
                }

                private void QueueContinuation(Action continuation, bool flowContext)
                {
                    _factory.StartNew(continuation);
                }

                public void GetResult() { } // Nop. It exists purely because the compiler pattern demands it.
            }
        }
    }
}
