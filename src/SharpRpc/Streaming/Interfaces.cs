// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface IStreamMessageFactory
    {
        IStreamCloseMessage CreateCloseMessage(string streamId);
        IStreamCancelMessage CreateCancelMessage(string streamId);
        IStreamCloseAckMessage CreateCloseAcknowledgement(string streamId);
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

    public interface IStreamAuxMessage : IGeneratedInteropMessage
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

    // The writer sends this message when the queue is empty and completed to additions. 
    // Not further message should be sent to the reader after this one.
    public interface IStreamCloseMessage : IStreamAuxMessage
    {
        StreamCloseOptions Options { get; set; }
    }

    public interface IStreamCloseAckMessage : IStreamAuxMessage
    {
    }

    [Flags]
    public enum StreamCloseOptions
    {
        None = 0,
        SendAcknowledgment = 1
    }

    // The reader may send this message when the read was canceled.
    // In response to this message the writer should:
    //      1) forbid additions to the queue;
    //      2) send all previously enqueued data;
    //      3) send StreamCloseMessage;
    //      4) wait for StreamCloseAcknowledgmentMessage (if requested);
    public interface IStreamCancelMessage : IStreamAuxMessage
    {
        StreamCancelOptions Options { get; set; }
    }

    [Flags]
    public enum StreamCancelOptions
    {
        None = 0,
        DropRemainingItems = 1
    }

    public interface IStreamPageAck : IStreamAuxMessage
    {
        ushort Consumed { get; set; }
    }

#if NET5_0_OR_GREATER
    public interface StreamReader<T> : IAsyncEnumerable<T>
#else
    public interface StreamReader<T>
#endif
    {
        IStreamEnumerator<T> GetEnumerator(CancellationToken cancellationToken = default);
    }

    public interface StreamWriter<T>
    {
#if NET5_0_OR_GREATER
        ValueTask<RpcResult> WriteAsync(T item);
        ValueTask<RpcResult> BulkWrite(ReadOnlyMemory<T> items);
#else
        Task<RpcResult> WriteAsync(T item);
        Task<RpcResult> BulkWrite(ArraySegment<T> items);
#endif

        Task<RpcResult> CompleteAsync();

        void EnableCancellation(CancellationToken cancelToken);
    }

    internal interface IStreamWriterFixture<T> : StreamWriter<T>
    {
        Task Closed { get; }

        void AllowSend();
        void Abort(RpcResult fault);
        bool OnMessage(IInteropMessage auxMessage, out RpcResult result);
    }

    internal interface IStreamReaderFixture<T> : StreamReader<T>
    {
        Task Closed { get; }

        void Abort(RpcResult fault);
        void Cancel(bool dropRemItems);
        bool OnMessage(IInteropMessage auxMessage, out RpcResult result);
    }
}
