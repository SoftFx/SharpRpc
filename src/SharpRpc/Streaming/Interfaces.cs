// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface IStreamMessageFactory
    {
        IStreamCompletionMessage CreateCompletionMessage(string streamId);
        IStreamPageAck CreatePageAcknowledgement(string streamId);
    }

    public interface IStreamMessageFactory<T> : IStreamMessageFactory
    {
        IStreamPage<T> CreatePage(string streamId);
    }

    public interface IOpenStreamRequest : IRequestMessage
    {
        ushort? WindowSize { get; set; }
    }

    public interface IStreamAuxMessage : IInteropMessage
    {
        //string StreamId { get; }
    }

    public interface IStreamPage : IStreamAuxMessage
    {
    }

    public interface IStreamPage<T> : IStreamPage
    {
        List<T> Items { get; set; }
    }

    public interface IStreamCompletionMessage : IStreamAuxMessage
    {
    }

    public interface IStreamPageAck : IStreamAuxMessage
    {
        ushort Consumed { get; set; }
    }

    public interface StreamReader<T>
    {
#if NET5_0_OR_GREATER
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);
#endif
    }

    public interface StreamWriter<T>
    {
#if NET5_0_OR_GREATER
        ValueTask<RpcResult> WriteAsync(T item);
#else
        Task<RpcResult> WriteAsync(T item);
#endif

        Task<RpcResult> CompleteAsync();
    }

    //public interface IStreamRxAck
    //{
    //    string StreamId { get; }
    //    int ConsumedItems { get; set; }
    //    int ConsumedBytes { get; set; }
    //}
}
