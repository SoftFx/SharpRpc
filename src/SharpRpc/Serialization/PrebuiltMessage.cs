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
    public abstract class PrebuiltMessage : IPrebuiltMessage
    {
        private readonly SegmentedByteArray _msgBytes;

        public PrebuiltMessage(SegmentedByteArray bytes)
        {
            _msgBytes = bytes;
        }

        public abstract string ContractMessageName { get; }

        public void WriteTo(ushort serializedId, MessageWriter writer)
        {
            foreach (var segment in _msgBytes)
            {
#if NET5_0_OR_GREATER
                writer.ByteStream.Write(segment);
#else
                writer.ByteStream.Write(segment.Array, segment.Offset, segment.Count);
#endif
            }
        }
    }

    public abstract class MultiPrebuiltMessage : IPrebuiltMessage
    {
        private readonly List<SegmentedByteArray> _msgBytesPerSerializer;

        public MultiPrebuiltMessage(List<SegmentedByteArray> bytesToSerializerMap)
        {
            _msgBytesPerSerializer = bytesToSerializerMap;
        }

        public abstract string ContractMessageName { get; }

        public void WriteTo(ushort serializedId, MessageWriter writer)
        {
            var msgBytes = _msgBytesPerSerializer[serializedId];

            foreach (var segment in msgBytes)
            {
#if NET5_0_OR_GREATER
                writer.ByteStream.Write(segment);
#else
                writer.ByteStream.Write(segment.Array, segment.Offset, segment.Count);
#endif
            }
        }
    }
}
