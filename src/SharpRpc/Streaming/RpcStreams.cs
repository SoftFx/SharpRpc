// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    internal static class RpcStreams
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
    }
}
