// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
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
#else
        public static async Task WaitAsync(this Task task, TimeSpan timeout)
        {
            using (var timeoutSrc = new CancellationTokenSource(timeout))
            {
                var timeoutTask = Task.Delay(Timeout.Infinite, timeoutSrc.Token);
                var finished = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);

                if (finished == task)
                {
                    timeoutSrc.Cancel();
                    await task.ConfigureAwait(false);
                }
                else
                    throw new TimeoutException();
            }
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

        public static Task OnFaulted(this Task task, Action<Exception> action)
        {
            return task.ContinueWith(t =>
            {
                var taskEx = t.Exception;
                Exception ex = taskEx;
                if (taskEx?.InnerExceptions != null && taskEx.InnerExceptions.Count == 1)
                    ex = taskEx.InnerExceptions[0];

                action(ex);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
