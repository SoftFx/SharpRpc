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
        public static ValueTask AsyncVoid => new ValueTask();
        public static ValueTask<bool> AsyncTrue => ValueTask.FromResult(true);
        public static ValueTask<bool> AsyncFalse => ValueTask.FromResult(false);
        public static ValueTask<RpcResult> AsyncRpcOk => ValueTask.FromResult(RpcResult.Ok);
        public static ValueTask<List<IMessage>> AsyncNullMessageBatch => ValueTask.FromResult<List<IMessage>>(null);

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
#else
        public static Task AsyncVoid => Task.CompletedTask;
        public static Task<bool> AsyncTrue = Task.FromResult(true);
        public static Task<bool> AsyncFalse = Task.FromResult(false);
        public static Task<RpcResult> AsyncRpcOk => Task.FromResult(RpcResult.Ok);

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
