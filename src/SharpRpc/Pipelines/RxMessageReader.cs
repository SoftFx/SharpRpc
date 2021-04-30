// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    internal class RxMessageReader : MessageReader
    {
        private readonly BufferSequence<byte> _bsAdapter = new BufferSequence<byte>();

        public void Init(IReadOnlyList<ArraySegment<byte>> segments)
        {
            _bsAdapter.AddRange(segments);
        }

        public int MsgSize => _bsAdapter.Count;

        public void Clear()
        {
            _bsAdapter.Clear();
        }

        public ReadOnlySequence<byte> ByteBuffer => _bsAdapter.GetSequence();

        public Stream ByteStream => throw new NotImplementedException();
    }
}
