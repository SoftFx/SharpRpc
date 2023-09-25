// Copyright © 2022 Soft-Fx. All rights reserved.
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

namespace SharpRpc.Streaming
{
    internal class BinaryStreamReader : StreamReaderBase<byte, ArraySegment<byte>>
    {
        public BinaryStreamReader(string callId, TxPipeline tx, IStreamMessageFactory factory, IRpcLogger logger) : base(callId, tx, factory, logger)
        {
        }

        protected override bool IsNull(ArraySegment<byte> page) => page.Array == null;
        protected override byte GetItem(ArraySegment<byte> page, int index) => page.Array[page.Offset + index];
        protected override int GetItemsCount(ArraySegment<byte> page) => page.Count;

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
    }
}
