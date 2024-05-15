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
using System.Threading.Tasks;

namespace SharpRpc
{
    public static class FwAdapter
    {
#if NET5_0_OR_GREATER
        public static ValueTask AsyncVoid { get; } = new ValueTask();
        public static ValueTask<bool> AsyncTrue { get; } = ValueTask.FromResult(true);
        public static ValueTask<bool> AsyncFalse { get; } = ValueTask.FromResult(false);
        public static ValueTask<RpcResult> AsyncRpcOk { get; } = ValueTask.FromResult(RpcResult.Ok);
        public static ValueTask<List<IMessage>> AsyncNullMessageBatch { get; } = ValueTask.FromResult<List<IMessage>>(null);

        public static ValueTask WrappResult(Task task)
        {
            return new ValueTask(task);
        }

        public static ValueTask<T> WrappResult<T>(Task<T> asyncVal)
        {
            return new ValueTask<T>(asyncVal);
        }

        public static ValueTask WrappException(Exception ex)
        {
            return ValueTask.FromException(ex);
        }

        public static ValueTask<T> WrappResult<T>(T val)
        {
            return ValueTask.FromResult<T>(val);
        }

        public static Task ToTask(this ValueTask task)
        {
            return task.AsTask();
        }

        public static Task<T> ToTask<T>(this ValueTask<T> valueTask)
        {
            return valueTask.AsTask();
        }

        public static void Wait(this ValueTask vTask)
        {
            if (!vTask.IsCompleted)
                vTask.AsTask().Wait();
        }

        public static bool Wait(this ValueTask vTask, int millisecondsTimeout)
        {
            if (!vTask.IsCompleted)
                return vTask.AsTask().Wait(millisecondsTimeout);
            return true;
        }

#else
        public static Task AsyncVoid { get; } = Task.CompletedTask;
        public static Task<bool> AsyncTrue { get; } = Task.FromResult(true);
        public static Task<bool> AsyncFalse { get; } = Task.FromResult(false);
        public static Task<RpcResult> AsyncRpcOk { get; } = Task.FromResult(RpcResult.Ok);

        public static Task WrappResult(Task task)
        {
            return task;
        }

        public static Task<T> WrappResult<T>(Task<T> asyncVal)
        {
            return asyncVal;
        }

        public static Task WrappException(Exception ex)
        {
            return Task.FromException(ex);
        }

        public static Task<T> WrappResult<T>(T val)
        {
            return Task.FromResult<T>(val);
        }

        public static Task ToTask(this Task task)
        {
            return task;
        }

        public static Task<T> ToTask<T>(this Task<T> valueTask)
        {
            return valueTask;
        }
#endif
    }
}
