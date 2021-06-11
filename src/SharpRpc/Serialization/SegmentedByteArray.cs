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
    public class SegmentedByteArray : List<ArraySegment<byte>>, IDisposable
    {
        public void WriteAllTo(MessageWriter writer)
        {
            foreach (var segment in this)
            {
#if NET5_0_OR_GREATER
                writer.ByteStream.Write(segment);
#else
                writer.ByteStream.Write(segment.Array, segment.Offset, segment.Count);
#endif
            }
        }

        public void Dispose()
        {
#if NET5_0_OR_GREATER
            foreach (var segment in this)
                System.Buffers.ArrayPool<byte>.Shared.Return(segment.Array);
#endif

            Clear();
        }
    }
}
