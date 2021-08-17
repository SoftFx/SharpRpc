// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    //public interface OutputStreamHandler<TItem>
    //{
    //    PagingTxStream<TItem> OutputStream { get; }
    //}

    //public interface InputStreamHandler<TItem>
    //{
    //    PagingRxStream<TItem> InputStream { get; }
    //}

    //public interface DuplexStreamHandler<TInItem, TOutItem>
    //{
    //    PagingRxStream<TInItem> InputStream { get; }
    //    PagingTxStream<TOutItem> OutputStream { get; }
    //}

    public class StreamHandler<TInItem, TOutItem> : MessageDispatcherCore.IInteropOperation //: InputStreamHandler<TInItem>, OutputStreamHandler<TOutItem>
    {
        public StreamHandler(IOpenStreamRequest request, Channel ch, IStreamMessageFactory<TInItem> inFactory, IStreamMessageFactory<TOutItem> outFactory)
        {
            CallId = request.CallId;
            if (inFactory != null)
                InputStream = new PagingRxStream<TInItem>(inFactory);
            if (outFactory != null)
                OutputStream = new PagingTxStream<TOutItem>(CallId, ch, outFactory, 10, 10);
        }

        public string CallId { get; }
        public PagingRxStream<TInItem> InputStream { get; }
        public PagingTxStream<TOutItem> OutputStream { get; }

        public void Close()
        {
        }

        RpcResult MessageDispatcherCore.IInteropOperation.Complete(IResponse respMessage)
        {
            return new RpcResult(RpcRetCode.ProtocolViolation, "");
        }

        void MessageDispatcherCore.IInteropOperation.Fail(RpcResult result)
        {
        }

        void MessageDispatcherCore.IInteropOperation.Fail(IRequestFault faultMessage)
        {
        }

        RpcResult MessageDispatcherCore.IInteropOperation.Update(IStreamPage page)
        {
            var typedPage = page as IStreamPage<TInItem>;

            if (typedPage == null)
                return new RpcResult(RpcRetCode.ProtocolViolation, "");

            InputStream.OnRx(typedPage);
            return RpcResult.Ok;
        }
    }
}
