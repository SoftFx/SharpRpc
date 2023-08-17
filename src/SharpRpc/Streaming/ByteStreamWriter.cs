// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    //internal class ByteStreamWriter : Stream
    //{
    //    private readonly object _lockObj = new object();
    //    private readonly IRpcLogger _logger;
    //    //private readonly Queue<IStreamPage<T>> _queueCompletePages = new Queue<IStreamPage<T>>();
    //    private readonly TxPipeline _msgTransmitter;
    //    //private readonly Queue<EnqueueAwaiter> _enqueueAwaiters = new Queue<EnqueueAwaiter>();
    //    //private readonly List<EnqueueAwaiter> _awaitersToRelease = new List<EnqueueAwaiter>();
    //    private readonly TaskCompletionSource<RpcResult> _closedEventSrc = new TaskCompletionSource<RpcResult>();
    //    //private bool _isClosed;
    //    //private bool _isAbroted;
    //    private bool _isSendingEnabled;
    //    private RpcResult _closeFault;
    //    private bool _isSedning;
    //    private int _windowSize;
    //    //private readonly IStreamMessageFactory<T> _factory;
    //    private readonly StreamWriteCoordinator _coordinator;
    //    private CancellationTokenRegistration _cancelReg;
    //    private bool _isCancellationEnabled;
    //    private string _name;

    //    #region State management

    //    internal void AllowSend()
    //    {
    //        //lock (_lockObj)
    //        //{
    //        //    _isSendingEnabled = true;

    //        //    if (!_isSedning && DataIsAvailable && !_coordinator.IsBlocked)
    //        //    {
    //        //        _isSedning = true;
    //        //        _pageToSend = DequeuePage();
    //        //    }
    //        //    else
    //        //        return;
    //        //}

    //        //SendNextPage();
    //    }

    //    // Complete writes and close communication gracefully (send Close -> wait for CloseAck);
    //    //internal void Close(RpcResult fault)
    //    //{
    //    //    CloseStream(false, false, fault);
    //    //}

    //    // Close the writer immediately without any further messaging.
    //    internal void Abort(RpcResult fault)
    //    {
    //        CloseStream(true, true, fault, "Aborted.");
    //    }

    //    internal void Cancel()
    //    {
    //        CloseStream(false, false, new RpcResult(RpcRetCode.OperationCanceled, "The operation was canceled by the user!"),
    //            "Cancellation is requested.");
    //    }

    //    private void CloseStream(bool abort, bool dropQueue, RpcResult fault, string closeReason)
    //    {
    //        //var sendCloseMessage = false;

    //        //lock (_lockObj)
    //        //{
    //        //    if (State == States.Online || abort) // allow abortion when normal completion is already being in the process
    //        //        CloseStreamInternal(abort, dropQueue, fault, closeReason, out sendCloseMessage);
    //        //}

    //        //if (sendCloseMessage)
    //        //    SendCloseMessage();
    //    }

    //    internal void OnRx(IStreamCancelMessage cancelMsg)
    //    {
    //        CloseStream(false, cancelMsg.Options.HasFlag(StreamCancelOptions.DropRemainingItems), RpcResult.OperationCanceled,
    //            "Received a cancel message.");
    //    }

    //    internal RpcResult OnRx(IStreamCloseAckMessage closeAckMsg)
    //    {
    //        throw new NotImplementedException();
    //        //lock (_lockObj)
    //        //{
    //        //    if (State != States.Completed)
    //        //        return RpcResult.UnexpectedMessage(closeAckMsg.GetType(), GetType()); // signal protocol violation

    //        //    ChangeState(States.Closed);

    //        //    if (_logger.VerboseEnabled)
    //        //        _logger.Verbose(GetName(), "Received a close acknowledgment. [Closed]");

    //        //    return RpcResult.Ok;
    //        //}
    //    }

    //    #endregion

    //    #region Transmission

    //    private Task Write(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    #endregion

    //    #region Stream implementation

    //    public override bool CanRead => false;
    //    public override bool CanSeek => false;
    //    public override bool CanWrite => true;
    //    public override long Length => throw new NotSupportedException();
    //    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    //    public override void Flush()
    //    {
    //    }

    //    public override Task FlushAsync(CancellationToken cancellationToken)
    //    {
    //        return base.FlushAsync(cancellationToken);
    //    }

    //    public override void Write(byte[] buffer, int offset, int count)
    //    {
    //        Write(buffer, offset, count, CancellationToken.None).Wait();
    //    }

    //    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    //    {
    //        return Write(buffer, offset, count, cancellationToken);
    //    }

    //    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    //    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    //    public override void SetLength(long value) => throw new NotSupportedException();

    //    #endregion
    //}
}
