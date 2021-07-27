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

namespace SharpRpc.MsTest.MockObjects
{
    public class MockPrebuiltMessage : IPrebuiltMessage
    {
        public MockPrebuiltMessage(int size)
        {
            RawBytes = new byte[size];
        }

        public byte[] RawBytes { get; }

        public static MockPrebuiltMessage Generate(int size)
        {
            var msg = new MockPrebuiltMessage(size);

            for (int i = 0; i < size; i++)
                msg.RawBytes[i] = (byte)(i % byte.MaxValue);

            return msg;
        }

        public void WriteTo(ushort serializedId, MessageWriter writer)
        {
            writer.ByteStream.Write(RawBytes);
        }
    }
}
