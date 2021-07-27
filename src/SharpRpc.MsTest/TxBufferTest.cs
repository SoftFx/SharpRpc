// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRpc.MsTest.MockObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.MsTest
{
    [TestClass]
    public class TxBufferTest
    {
        [TestMethod]
        public void TxBuffer_NoOverflow()
        {
            var buffer = new TxBuffer(new object(), 200, new BufferBasedMockSerializer());
            var msg = MockMessage.Generate(100);

            var expectedHeader = new byte[] { 1, 0, 103 };
            var expectedBody = msg.RawBytes;
            var expectedBytes = expectedHeader.Add(expectedBody);

            buffer.WriteMessage(msg);
            var segment = buffer.DequeueNext().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(expectedBytes, segment.ToArray());
        }

        [DataTestMethod]
        [DataRow(100, 115)]
        [DataRow(100, 99)]
        [DataRow(100, 98)]
        public void TxBuffer_WriteMessage_2Segments(int segmentSize, int messageSize)
        {
            var bodySize1 = segmentSize - 3;
            var bodySize2 = messageSize - bodySize1;

            var buffer = new TxBuffer(new object(), segmentSize, new BufferBasedMockSerializer());
            var msg = MockMessage.Generate(messageSize);

            var expectedHeader1 = new byte[] { (byte)MessageFlags.None, 0, (byte)segmentSize };
            var expectedBody1 = msg.RawBytes.Slice(0, bodySize1);
            var expectedSegment1 = expectedHeader1.Add(expectedBody1);

            var expectedHeader2 = new byte[] { (byte)(MessageFlags.EndOfMessage), 0, (byte)(bodySize2 + 3) };
            var expectedBody2 = msg.RawBytes.Slice(bodySize1, bodySize2);
            var expectedSegment2 = expectedHeader2.Add(expectedBody2);

            buffer.WriteMessage(msg);

            var resultingSegments = new List<ArraySegment<byte>>();
            resultingSegments.Add(buffer.DequeueNext().GetAwaiter().GetResult());
            resultingSegments.Add(buffer.DequeueNext().GetAwaiter().GetResult());

            Assert.AreEqual(2, resultingSegments.Count);
            CollectionAssert.AreEqual(expectedSegment1, resultingSegments[0].ToArray());
            CollectionAssert.AreEqual(expectedSegment2, resultingSegments[1].ToArray());
        }

        [DataTestMethod]
        [DataRow(100, 115)]
        [DataRow(100, 99)]
        [DataRow(100, 98)]
        public void TxBuffer_WritePrebuiltMessage_2Segments(int segmentSize, int messageSize)
        {
            var bodySize1 = segmentSize - 3;
            var bodySize2 = messageSize - bodySize1;

            var buffer = new TxBuffer(new object(), segmentSize, new BufferBasedMockSerializer());
            var msg = MockPrebuiltMessage.Generate(messageSize);

            var expectedHeader1 = new byte[] { (byte)MessageFlags.None, 0, (byte)segmentSize };
            var expectedBody1 = msg.RawBytes.Slice(0, bodySize1);
            var expectedSegment1 = expectedHeader1.Add(expectedBody1);

            var expectedHeader2 = new byte[] { (byte)(MessageFlags.EndOfMessage), 0, (byte)(bodySize2 + 3) };
            var expectedBody2 = msg.RawBytes.Slice(bodySize1, bodySize2);
            var expectedSegment2 = expectedHeader2.Add(expectedBody2);

            buffer.WriteMessage(msg);

            var resultingSegments = new List<ArraySegment<byte>>();
            resultingSegments.Add(buffer.DequeueNext().GetAwaiter().GetResult());
            resultingSegments.Add(buffer.DequeueNext().GetAwaiter().GetResult());

            Assert.AreEqual(2, resultingSegments.Count);
            CollectionAssert.AreEqual(expectedSegment1, resultingSegments[0].ToArray());
            CollectionAssert.AreEqual(expectedSegment2, resultingSegments[1].ToArray());
        }
    }
}
