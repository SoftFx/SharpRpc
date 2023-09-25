// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Streaming
{
    public static class BinaryStreamExt
    {
#if NET5_0_OR_GREATER
        public static async ValueTask<RpcResult> TryWriteAllAsync(this StreamWriter<byte> writer, Stream stream)
#else
        public static async Task<RpcResult> TryWriteAllAsync(this StreamWriter<byte> writer, Stream stream)
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
    }
}
