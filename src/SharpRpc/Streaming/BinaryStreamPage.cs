// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    internal class BinaryStreamPage : IBinaryMessage, IInteropMessage
    {
        public BinaryStreamPage(string callId, ArraySegment<byte> data)
        {
            CallId = callId;
            Data = data;
        }

        public string CallId { get; }
        public ArraySegment<byte> Data { get; }

        public void WriteTo(MessageWriter writer)
        {
            WriteHeader(writer, CallId);
            WriteBody(writer, Data);
        }

        public static void WriteHeader(MessageWriter writer, string callId)
        {
            var callIdSize = Encoding.UTF8.GetByteCount(callId);
            var headerSize = callIdSize + 3;

            var buffer = writer.AllocateWriteBuffer(headerSize);
            var index = buffer.Offset;

            buffer.Array[index++] = 1; // stream page message Id
            BitTools.Instance.Write((ushort)callIdSize, buffer.Array, ref index); // CallId length
            Encoding.UTF8.GetBytes(callId, 0, callId.Length, buffer.Array, index); // CallId bytes

            writer.AdvanceWriteBuffer(headerSize);
        }

        public static void WriteBody(MessageWriter writer, ArraySegment<byte> data)
        {
            var toWrite = data.Count;
            var offset = data.Offset;

            while (toWrite > 0)
            {
                var buffer = writer.AllocateWriteBuffer();
                var copySize = Math.Min(toWrite, buffer.Count);
                Array.Copy(data.Array, offset, buffer.Array, buffer.Offset, copySize);
                writer.AdvanceWriteBuffer(copySize);
                toWrite -= copySize;
                offset += copySize;
            }
        }

        public static RpcResult Read(RxMessageReader reader, out BinaryStreamPage page)
        {
            page = null;

            if (!reader.Se.TryReadString(out var callId, out var callIdLen))
                return new RpcResult(RpcRetCode.MessageMarkupError, "");

            var bodySize = reader.MessageSize - 1 - callIdLen;

            if (!reader.Se.TryReadByteArray(bodySize, out var bytes))
                return new RpcResult(RpcRetCode.MessageMarkupError, "");

            page = new BinaryStreamPage(callId, new ArraySegment<byte>(bytes));
            return RpcResult.Ok;
        }
    }
}
