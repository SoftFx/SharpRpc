// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    internal class BinaryStreamReader : StreamReaderBase<byte, ArraySegment<byte>>
    {
        public BinaryStreamReader(string callId, TxPipeline tx, IStreamMessageFactory factory, IRpcLogger logger) : base(callId, tx, factory, logger)
        {
#if NET5_0_OR_GREATER
            Pages = new PagesProxy(this);
#endif
        }

#if NET5_0_OR_GREATER
        public IAsyncEnumerable<ArraySegment<byte>> Pages { get; }

        private class PagesProxy : IAsyncEnumerable<ArraySegment<byte>>
        {
            private readonly BinaryStreamReader _reader;

            public PagesProxy(BinaryStreamReader reader)
            {
                _reader = reader;
            }

            public IAsyncEnumerator<ArraySegment<byte>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return _reader.CreatePageEnumerator(cancellationToken);
            }
        }
#endif

        protected override bool IsNull(ArraySegment<byte> page) => page.Array == null;
        protected override byte GetItem(ArraySegment<byte> page, int index) => page.Array[page.Offset + index];
        protected override int GetItemsCount(ArraySegment<byte> page) => page.Count;
        protected override void FreePage(ArraySegment<byte> page) => ArrayPool<byte>.Shared.Return(page.Array, false);
        protected override void CopyItems(ArraySegment<byte> page, int pageIndex, byte[] destArray, int destIndex, int count)
            => Array.Copy(page.Array, page.Offset + pageIndex, destArray, destIndex, count);

        internal override bool OnMessage(IInteropMessage auxMessage, out RpcResult result)
        {
            result = RpcResult.Ok;

            if (auxMessage is BinaryStreamPage page)
            {
                OnRx(page.Data);
                return true;
            }
            else if (auxMessage is IStreamCloseMessage closeMsg)
            {
                OnRx(closeMsg);
                return true;
            }

            return false;
        }

        public IStreamEnumerator<ArraySegment<byte>> GetPageEnumerator(CancellationToken cancellationToken = default)
        {
            return CreatePageEnumerator(cancellationToken);
        }

        private PageEnumerator CreatePageEnumerator(CancellationToken cancellationToken)
        {
            lock (LockObj) return SetEnumerator(new PageEnumerator(this, cancellationToken));
        }

        private void Free(ArraySegment<byte> page) => ArrayPool<byte>.Shared.Return(page.Array, false);

#if NET5_0_OR_GREATER
        private class PageEnumerator : AsyncEnumeratorBase, IAsyncEnumerator<ArraySegment<byte>>, IStreamEnumerator<ArraySegment<byte>>, IDisposable
#else
        private class PageEnumerator : AsyncEnumeratorBase, IStreamEnumerator<ArraySegment<byte>>, IDisposable
#endif
        {
            public PageEnumerator(StreamReaderBase<byte, ArraySegment<byte>> stream, CancellationToken cancellationToken)
                : base(stream, cancellationToken)
            {
            }

            public ArraySegment<byte> Current { get; private set; }

            public override NextItemCode GetNextItem(out IStreamPageAck pageAck)
            {
                if (Current.Array != null)
                    Stream.OnPageConsumed(new ArraySegment<byte>(Current.Array));

                var code = Stream.TryGetNextPage(out var page, out pageAck);
                Current = page;
                return code;
            }

#if NET5_0_OR_GREATER
            public override ValueTask DisposeAsync()
#else
            public override Task DisposeAsync()
#endif
            {
                if (Current.Array != null)
                {
                    Stream.OnPageConsumed(Current);
                    Current = default;
                }

                return base.DisposeAsync();
            }
        }
    }
}
