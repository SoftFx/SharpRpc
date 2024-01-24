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
    public class MockMessage : IMessage
    {
        public MockMessage(int size)
        {
            RawBytes = new byte[size];
        }

        public byte[] RawBytes { get; }

        public string ContractMessageName => nameof(MockMessage);

        public static MockMessage Generate(int size)
        {
            var msg = new MockMessage(size);

            for (int i = 0; i < size; i++)
                msg.RawBytes[i] = (byte)(i % byte.MaxValue);

            return msg;
        }
    }
}
