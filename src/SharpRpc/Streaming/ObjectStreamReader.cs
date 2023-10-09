// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace SharpRpc
{
    public interface IStreamEnumerator<T> : IDisposable
    {
        T Current { get; }
#if NET5_0_OR_GREATER
        ValueTask<bool> MoveNextAsync();
#else
        Task<bool> MoveNextAsync();
#endif
    }

    public interface IStreamBulkEnumerator<T> : IDisposable
    {
#if NET5_0_OR_GREATER
        ValueTask<RpcResult<int>> Read(ArraySegment<T> buffer);
        ValueTask<RpcResult<int>> GreedyRead(ArraySegment<T> buffer);
#else
        Task<RpcResult<int>> Read(ArraySegment<T> buffer);
        Task<RpcResult<int>> GreedyRead(ArraySegment<T> buffer);
#endif
    }

    public class ObjectStreamReader<T> : StreamReaderBase<T, IStreamPage<T>>
    {
        internal ObjectStreamReader(string callId, TxPipeline tx, IStreamMessageFactory factory, IRpcLogger logger) : base(callId, tx, factory, logger)
        {
        }

        protected override bool IsNull(IStreamPage<T> page) => page == null;
        protected override T GetItem(IStreamPage<T> page, int index) => page.Items[index];
        protected override int GetItemsCount(IStreamPage<T> page) => page.Items.Count;
        protected override void CopyItems(IStreamPage<T> page, int pageIndex, T[] destArray, int destIndex, int count) => throw new NotImplementedException();

        internal override bool OnMessage(IInteropMessage auxMessage, out RpcResult result)
        {
            result = RpcResult.Ok;

            if (auxMessage is IStreamPage<T> page)
            {
                OnRx(page);
                return true;
            }
            else if (auxMessage is IStreamCloseMessage closeMsg)
            {
                OnRx(closeMsg);
                return true;
            }

            return false;
        }
    }
}
