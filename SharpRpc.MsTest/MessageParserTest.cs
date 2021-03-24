using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.MsTest
{
    [TestClass]
    public class MessageParserTest
    {
        [TestMethod]
        public void ParserTest_NoOverflow()
        {
            var msgSize = 115;
            var msg = MockMessage.Generate(msgSize);
            var header = new byte[] { (byte)(MessageFlags.UserMessage | MessageFlags.EndOfMessage), 0, (byte)(msgSize + 3) };
            var segment = header.Add(msg.RawBytes);

            var parser = new MessageParser();
            parser.SetNextSegment(segment);
            var rCode = parser.ParseFurther();
            var parsedMsgBody = parser.MessageBody;

            var expectedBody = msg.RawBytes;
            //var expedctedMsgType = MessageType.User;
            var expectedRetCode = MessageParser.RetCodes.MessageParsed;

            Assert.AreEqual(expectedRetCode, rCode);
            //Assert.AreEqual(expedctedMsgType, parser.MessageType);
            Assert.AreEqual(1, parsedMsgBody.Count);
            CollectionAssert.AreEqual(expectedBody, parsedMsgBody[0].ToArray());
        }

        [DataTestMethod]
        [DataRow(55, 65)]
        [DataRow(59, 65)]
        [DataRow(60, 65)]
        [DataRow(61, 65)]
        [DataRow(62, 65)]
        [DataRow(70, 26)]
        [DataRow(50, 30)]
        public void ParserTest_2Chunks(int body1Size, int fragmentSize)
        {
            var msgSize = 115;
            //var body1Size = 50;
            var body2Size = msgSize - body1Size;

            var msg = MockMessage.Generate(msgSize);

            var header1 = new byte[] { (byte)MessageFlags.UserMessage, 0, (byte)(body1Size + 3) };
            var body1 = msg.RawBytes.Slice(0, body1Size);
            var segment1 = header1.Add(body1);

            var header2 = new byte[] { (byte)(MessageFlags.MessageContinuation | MessageFlags.EndOfMessage), 0, (byte)(body2Size + 3) };
            var body2 = msg.RawBytes.Slice(body1Size, body2Size);
            var segment2 = header2.Add(body2);

            var allBytes = segment1.Add(segment2);
            var fragments = allBytes.Partition(fragmentSize);

            var parser = new MessageParser();

            for (int i = 0; i < fragments.Count; i++)
            {
                parser.SetNextSegment(fragments[i]);
                var pCode = parser.ParseFurther();

                if (i != fragments.Count - 1)
                    Assert.AreEqual(MessageParser.RetCodes.EndOfSegment, pCode);
                else 
                    Assert.AreEqual(MessageParser.RetCodes.MessageParsed, pCode);
            }

            var parsedMsgBody = ArrayExt.Join(parser.MessageBody);
            var expectedBody = msg.RawBytes;

            CollectionAssert.AreEqual(expectedBody, parsedMsgBody);
        }
    }
}
