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

        public static MockMessage Generate(int size)
        {
            var msg = new MockMessage(size);

            for (int i = 0; i < size; i++)
                msg.RawBytes[i] = (byte)(i % byte.MaxValue);

            return msg;
        }
    }
}
