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
    public interface OutputStreamCall<TItem>
    {
        TxStream<TItem> OutputStream { get; }

        Task WaitCompletion();
        Task<RpcResult> TryWaitCompletion();
    }

    public interface OutputStreamCall<TItem, TResult>
    {
        TxStream<TItem> OutputStream { get; }

        Task<TResult> GetResult();
        Task<RpcResult<TResult>> TryGetResult();
    }

    public interface InputStreamCall<TItem>
    {
        RxStream<TItem> InputStream { get; }

        Task WaitCompletion();
        Task<RpcResult> TryWaitCompletion();
    }

    public interface InputStreamCall<TItem, TResult>
    {
        RxStream<TItem> InputStream { get; }

        Task<TResult> GetResult();
        Task<RpcResult<TResult>> TryGetResult();
    }

    public interface DuplexStreamCall<TInItem, TOutItem> : InputStreamCall<TInItem>, OutputStreamCall<TOutItem>
    {
    }

    public interface DuplexStreamCall<TInItem, TOutItem, TResult> : InputStreamCall<TInItem, TResult>, OutputStreamCall<TOutItem, TResult>
    {
    }

    internal class StreamCall<TInItem, TOutItem, TResult> :
        OutputStreamCall<TOutItem>, OutputStreamCall<TOutItem, TResult>,
        InputStreamCall<TInItem>, InputStreamCall<TInItem, TResult>,
        DuplexStreamCall<TInItem, TOutItem>, DuplexStreamCall<TInItem, TOutItem, TResult>
    {
        public RxStream<TInItem> InputStream { get; }
        public TxStream<TOutItem> OutputStream { get; }

        public Task WaitCompletion()
        {
            return Task.CompletedTask;
        }

        public Task<RpcResult> TryWaitCompletion()
        {
            return Task.FromResult(RpcResult.Ok);
        }

        public Task<TResult> GetResult()
        {
            return Task.FromResult(default(TResult));
        }

        public Task<RpcResult<TResult>> TryGetResult()
        {
            return Task.FromResult(RpcResult.FromResult(default(TResult)));
        }
    }
}
