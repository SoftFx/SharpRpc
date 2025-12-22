// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    internal static class TaskExt
    {
#if NET5_0_OR_GREATER
        public static ValueTask AsValueTask<T>(this ValueTask<T> valueTask)
        {
            if (valueTask.IsCompletedSuccessfully)
            {
                _ = valueTask.Result;// trigger exceptions
                return default;
            }

            return new ValueTask(valueTask.AsTask());
        }
#endif

        /// <summary>Doesn't actually cancels the task. Just wraps provided task into new one with cancellation token.</summary>
        public static Task WithCancelToken(this Task task, CancellationToken cToken)
        {
            static Task BypassTask(Task t) => t;
            return task.ContinueWith(BypassTask, cToken).Unwrap();
        }

        /// <summary>Doesn't actually cancels the task. Just wraps provided task into new one with cancellation token.</summary>
        public static Task<T> WithCancelToken<T>(this Task<T> task, CancellationToken cToken)
        {
            static Task<T> BypassTask(Task<T> t) => t;
            return task.ContinueWith(BypassTask, cToken).Unwrap();
        }
    }
}
