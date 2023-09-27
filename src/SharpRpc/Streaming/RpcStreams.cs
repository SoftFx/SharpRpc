// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    public static class RpcStreams
    {
        internal static IStreamWriterFixture<T> CreateWriter<T>(string callId, TxPipeline msgTransmitter, IStreamMessageFactory<T> factory,
            bool allowSending, StreamOptions options, IRpcLogger logger)
        {
            if (typeof(T) == typeof(byte))
                return (IStreamWriterFixture<T>)new BinaryStreamWriter(callId, msgTransmitter, factory, allowSending, options, logger);
            else
                return new ObjectStreamWriter<T>(callId, msgTransmitter, factory, allowSending, options, logger);
        }

        internal static IStreamReaderFixture<T> CreateReader<T>(string callId, TxPipeline tx, IStreamMessageFactory<T> factory, IRpcLogger logger)
        {
            if (typeof(T) == typeof(byte))
                return (IStreamReaderFixture<T>)new BinaryStreamReader(callId, tx, factory, logger);
            else
                return new ObjectStreamReader<T>(callId, tx, factory, logger);
        }

#if NET5_0_OR_GREATER
        public static async ValueTask<RpcResult> WriteAllAsync(this StreamWriter<byte> writer, Stream stream)
#else
        public static async Task<RpcResult> WriteAllAsync(this StreamWriter<byte> writer, Stream stream)
#endif
        {
            var binWriter = (BinaryStreamWriter)writer;

            while (true)
            {
                var startResult = await binWriter.StartBulkWrite();

                if (!startResult.IsOk)
                    return startResult;

                var buffer = startResult.Value;
                var bytesRead = await stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count);

                if (bytesRead == 0)
                    return RpcResult.Ok;

                binWriter.CommitBulkWrite(bytesRead);
            }
        }

#if NET5_0_OR_GREATER
        public static async ValueTask ReadAllAsync(this StreamReader<byte> reader, Stream targetStream)
#else
        public static async Task ReadAllAsync(this StreamReader<byte> reader, Stream targetStream)
#endif
        {
            var binReader = (BinaryStreamReader)reader;

            var e = binReader.GetPageEnumerator();

            while (await e.MoveNextAsync())
            {
                var segment = e.Current;
                await targetStream.WriteAsync(segment.Array, segment.Offset, segment.Count);
            }
        }
    }
}
