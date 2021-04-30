// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class RpcServiceBase : IUserMessageHandler
    {
        protected abstract ValueTask OnMessage(IMessage message);
        protected abstract ValueTask<IResponse> OnRequest(IRequest message);

        protected ValueTask OnUnknownMessage(IMessage message)
        {
            return new ValueTask();
        }

        protected ValueTask<IResponse> OnUnknownRequest(IRequest message)
        {
            throw new NotImplementedException();
        }

        protected IResponse CreateFaultResponse(Exception ex)
        {
            throw new NotImplementedException();
        }

        ValueTask IUserMessageHandler.ProcessMessage(IMessage message)
        {
            return OnMessage(message);
        }

        ValueTask<IResponse> IUserMessageHandler.ProcessRequest(IRequest message)
        {
            return OnRequest(message);
        }
    }
}
