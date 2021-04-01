using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            var resultingSegments = new List<ArraySegment<byte>>();

            var expectedHeader = new byte[] { 1, 0, 103 };
            var expectedBody = msg.RawBytes;
            var expectedBytes = expectedHeader.Add(expectedBody);

            buffer.WriteMessage(msg);
            buffer.ReturnAndDequeue(resultingSegments).AsTask().Wait();

            Assert.AreEqual(1, resultingSegments.Count);
            CollectionAssert.AreEqual(expectedBytes, resultingSegments[0].ToArray());
        }

        [DataTestMethod]
        [DataRow(100, 115)]
        [DataRow(100, 99)]
        [DataRow(100, 98)]
        public void TxBuffer_2Segments(int segmentSize, int messageSize)
        {
            var bodySize1 = segmentSize - 3;
            var bodySize2 = messageSize - bodySize1;

            var buffer = new TxBuffer(new object(), segmentSize, new BufferBasedMockSerializer());
            var msg = MockMessage.Generate(messageSize);
            var resultingSegments = new List<ArraySegment<byte>>();

            var expectedHeader1 = new byte[] { (byte)MessageFlags.None, 0, (byte)segmentSize };
            var expectedBody1 = msg.RawBytes.Slice(0, bodySize1);
            var expectedSegment1 = expectedHeader1.Add(expectedBody1);

            var expectedHeader2 = new byte[] { (byte)(MessageFlags.EndOfMessage), 0, (byte)(bodySize2 + 3) };
            var expectedBody2 = msg.RawBytes.Slice(bodySize1, bodySize2);
            var expectedSegment2 = expectedHeader2.Add(expectedBody2);

            buffer.WriteMessage(msg);
            buffer.ReturnAndDequeue(resultingSegments).AsTask().Wait();

            Assert.AreEqual(2, resultingSegments.Count);
            CollectionAssert.AreEqual(expectedSegment1, resultingSegments[0].ToArray());
            CollectionAssert.AreEqual(expectedSegment2, resultingSegments[1].ToArray());
        }
    }
}
