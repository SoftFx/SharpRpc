// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.MsTest
{
    public class BufferBasedMockSerializer : IRpcSerializer
    {
        public IMessage Deserialize(MessageReader reader)
        {
            var sReader = new System.Buffers.SequenceReader<byte>(reader.ByteBuffer);

            var msg = new MockMessage((int)sReader.Length);
            sReader.TryCopyTo(msg.RawBytes);

            return msg;
        }

        public void Serialize(IMessage message, MessageWriter writer)
        {
            var offset = 0;
            var msgBytes = ((MockMessage)message).RawBytes;

            while (offset < msgBytes.Length)
            {
                var mem = writer.ByteBuffer.GetMemory().Span;
                var remSize = msgBytes.Length - offset;
                var advanceSize = Math.Min(mem.Length, remSize);

                for (int i = 0; i < advanceSize; i++)
                    mem[i] = msgBytes[i + offset];

                offset += advanceSize;

                writer.ByteBuffer.Advance(advanceSize);
            }
        }
    }
}
