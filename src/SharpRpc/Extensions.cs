// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal static class Extensions
    {
        public static async Task<RpcResult> TryReceiveExact(this ByteTransport transport, byte[] buffer, int count, CancellationToken cToken)
        {
            var rxCount = 0;

            while (rxCount < count)
            {
                try
                {
                    var bytes = await transport.Receive(new ArraySegment<byte>(buffer, rxCount, count - rxCount), cToken).ConfigureAwait(false);

                    if (bytes == 0)
                        return new RpcResult(RpcRetCode.ConnectionAbortedByPeer, "The connection was closed by oher side.");

                    rxCount += bytes;
                }
                catch (OperationCanceledException)
                {
                    return RpcResult.OperationCanceled;
                }
                catch (Exception ex)
                {
                    return transport.TranslateException(ex);
                }
            }

            return RpcResult.Ok;
        }

        public static async Task<RpcResult> TrySend(this ByteTransport transport, ArraySegment<byte> data, CancellationToken cancelToken)
        {
            try
            {
                await transport.Send(data, cancelToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return RpcResult.OperationCanceled;
            }
            catch (Exception ex)
            {
                return transport.TranslateException(ex);
            }

            return RpcResult.Ok;
        }
    }
}
