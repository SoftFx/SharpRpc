// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Tcp
{
    /// <summary>
    /// On .Net Framework <see cref="SocketTaskExtensions.SendAsync"/>, <see cref="SocketTaskExtensions.ReceiveAsync"/> have 2 problems:<br/>
    /// 1. Pinned user buffers are kept between calls by cached SocketAsyncEventArgs.<br/>
    /// 2. Cached <see cref="SocketAsyncEventArgs"/> are not disposed, leaving pinned buffers and overlapped structures in memory forever.<br/>
    /// This class is implementing similar methods, allowing to control pinned buffers release and <see cref="SocketAsyncEventArgs"/> disposal.
    /// </summary>
    internal class SocketAsyncMethods : IDisposable
    {
        private readonly bool _keepPinnedBuffers;

        private bool _disposed;
        private ReusableSocketAsyncEventArgs _receiveArgs, _sendArgs;
        //private ReusableSocketAsyncEventArgs _receiveBufferListArgs, _sendBufferListArgs;


        public SocketAsyncMethods(bool keepPinnedBuffers)
        {
            _keepPinnedBuffers = keepPinnedBuffers;
            _receiveArgs = new ReusableSocketAsyncEventArgs(this, AsyncArgsCacheSlot.Receive);
            _sendArgs = new ReusableSocketAsyncEventArgs(this, AsyncArgsCacheSlot.Send);
        }


        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Interlocked.Exchange(ref _receiveArgs, null)?.Dispose();
            Interlocked.Exchange(ref _sendArgs, null)?.Dispose();
        }


        public Task<int> ReceiveAsync(Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested)
                return Task.FromCanceled<int>(cToken);

            var args = RentCachedArgs(ref _receiveArgs);
            return args.SendReceiveAsync(socket, true, buffer, socketFlags, cToken);
        }

        public Task<int> SendAsync(Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, CancellationToken cToken)
        {
            if (cToken.IsCancellationRequested)
                return Task.FromCanceled<int>(cToken);

            var args = RentCachedArgs(ref _sendArgs);
            return args.SendReceiveAsync(socket, false, buffer, socketFlags, cToken);
        }

        public static Task<int> ReceiveAsyncApm(Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, CancellationToken cToken)
        {
            return Task.Factory.FromAsync(BeginReceiveWrapper, socket.EndReceive, socket, buffer, socketFlags, null)
                .WithCancelToken(cToken);

            static IAsyncResult BeginReceiveWrapper(Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, AsyncCallback callback, object state)
            {
                return socket.BeginReceive(buffer.Array, buffer.Offset, buffer.Count, socketFlags, callback, state);
            }
        }

        public static Task<int> SendAsyncApm(Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, CancellationToken cToken)
        {
            return Task.Factory.FromAsync(BeginSendWrapper, socket.EndSend, socket, buffer, socketFlags, null)
                .WithCancelToken(cToken);

            static IAsyncResult BeginSendWrapper(Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, AsyncCallback callback, object state)
            {
                return socket.BeginSend(buffer.Array, buffer.Offset, buffer.Count, socketFlags, callback, state);
            }
        }


        private ReusableSocketAsyncEventArgs RentCachedArgs(ref ReusableSocketAsyncEventArgs argsLocation)
        {
            var args = Interlocked.Exchange(ref argsLocation, null);
            // in case of concurrent request cached value can be already taken
            args ??= new ReusableSocketAsyncEventArgs(this, AsyncArgsCacheSlot.SingleUse);
            return args;
        }

        private void ReturnCachedArgs(ReusableSocketAsyncEventArgs args, AsyncArgsCacheSlot cacheSlot)
        {
            if (_disposed || cacheSlot == AsyncArgsCacheSlot.SingleUse)
            {
                args.Dispose();
                return;
            }

            switch (cacheSlot)
            {
                case AsyncArgsCacheSlot.Receive:
                    ReturnCachedArgs(ref _receiveArgs, args);
                    break;
                case AsyncArgsCacheSlot.Send:
                    ReturnCachedArgs(ref _sendArgs, args);
                    break;
            }
        }

        private void ReturnCachedArgs(ref ReusableSocketAsyncEventArgs argsLocation, ReusableSocketAsyncEventArgs args)
        {
            // Buffers are pinned. Releasing single use buffers will be better for GC performance.
            // But if buffers are reused in multiple subsequent calls native overlapped structures will be created multiple times.
            if (!_keepPinnedBuffers)
                args.ReleaseBuffers();

            var replacedValue = Interlocked.CompareExchange(ref argsLocation, args, null);
            // Error condition, most likely concurrency issue.
            Debug.Assert(replacedValue == null, "Duplicate ReusableSocketAsyncEventArgs detected.");
            if (replacedValue != null)// dispose unused object
                args.Dispose();
        }

        private static Exception GetException(SocketError error) => new SocketException((int)error);


        private enum AsyncArgsCacheSlot
        {
            SingleUse = 0,
            Receive = 1,
            Send = 2,
            //ReceiveBufferList = 3,
            //SendBufferList = 4,
        }

        private class ReusableSocketAsyncEventArgs : IDisposable
        {
            private readonly SocketAsyncMethods _owner;
            private readonly AsyncArgsCacheSlot _cacheSlot;
            private readonly SocketAsyncEventArgs _socketArgs;

            private TaskCompletionSource<int> _taskSrc;
            private bool _disposed;


            public ReusableSocketAsyncEventArgs(SocketAsyncMethods owner, AsyncArgsCacheSlot cacheSlot)
            {
                _owner = owner;
                _cacheSlot = cacheSlot;
                _socketArgs = new SocketAsyncEventArgs();
                _socketArgs.Completed += OnIOCompleted;
            }


            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _socketArgs.Completed -= OnIOCompleted;
                _socketArgs.Dispose();
                _taskSrc?.TrySetException(new ObjectDisposedException(nameof(ReusableSocketAsyncEventArgs)));
            }

            public Task<int> SendReceiveAsync(Socket socket, bool receive, ArraySegment<byte> buffer, SocketFlags socketFlags, CancellationToken cToken)
            {
                SetBuffer(buffer);
                _socketArgs.SocketFlags = socketFlags;
                // IO completion can be executed very fast. Cache task source in advance.
                var taskSrc = InitTaskSrc();
                var pending = receive ? socket.ReceiveAsync(_socketArgs) : socket.SendAsync(_socketArgs);
                return pending ? taskSrc.Task.WithCancelToken(cToken) : HandleSyncResult(_socketArgs);
            }

            public void ReleaseBuffers()
            {
                ThrowIfDisposed();

                if (_socketArgs.Buffer != null)
                    _socketArgs.SetBuffer(null, 0, 0);
                if (_socketArgs.BufferList != null)
                    _socketArgs.BufferList = null;
            }


            private void SetBuffer(ArraySegment<byte> buffer)
            {
                ThrowIfDisposed();

                if (_socketArgs.BufferList != null)
                    _socketArgs.BufferList = null;

                _socketArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            }

            private void SetBufferList(IList<ArraySegment<byte>> bufferList)
            {
                ThrowIfDisposed();

                if (_socketArgs.Buffer != null)
                    _socketArgs.SetBuffer(null, 0, 0);

                _socketArgs.BufferList = bufferList;
            }

            private TaskCompletionSource<int> InitTaskSrc()
            {
                // If previous operation completed synchronously we will have task source remaining
                Debug.Assert(_taskSrc == null || _taskSrc.Task.Status == TaskStatus.WaitingForActivation, "Invalid task source state.");
                if (_taskSrc == null || _taskSrc.Task.Status != TaskStatus.WaitingForActivation)
                    _taskSrc = new TaskCompletionSource<int>();

                return _taskSrc;
            }

            private Task<int> HandleSyncResult(SocketAsyncEventArgs args)
            {
                var socketError = args.SocketError;
                var bytesTransferred = args.BytesTransferred;

                _owner.ReturnCachedArgs(this, _cacheSlot);

                if (socketError == SocketError.Success)
                    return Task.FromResult(bytesTransferred);
                else
                    return Task.FromException<int>(GetException(socketError));
            }

            private void OnIOCompleted(object sender, SocketAsyncEventArgs args)
            {
                var socketError = args.SocketError;
                var bytesTransferred = args.BytesTransferred;

                if (_disposed)
                    return;

                var taskSrc = _taskSrc;
                _taskSrc = null;
                _owner.ReturnCachedArgs(this, _cacheSlot);

                Debug.Assert(taskSrc != null, "IO completion concurrency issue: Empty task source.");
                if (socketError == SocketError.Success)
                    taskSrc?.TrySetResult(bytesTransferred);
                else
                    taskSrc?.TrySetException(GetException(socketError));
            }


            private void ThrowIfDisposed()
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ReusableSocketAsyncEventArgs));
            }
        }
    }
}
