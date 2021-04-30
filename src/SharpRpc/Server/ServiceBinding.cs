// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public class ServiceBinding
    {
        private readonly Func<RpcServiceBase> _serivceImplFactory;

        public ServiceBinding(Func<RpcServiceBase> serivceImplFactory, IRpcSerializer serializer, ISystemMessageFactory msgFactory)
        {
            _serivceImplFactory = serivceImplFactory;
            Descriptor = new ContractDescriptor(serializer, msgFactory);
        }

        internal ContractDescriptor Descriptor { get; }

        internal RpcServiceBase CreateServiceImpl()
        {
            return _serivceImplFactory();
        }
    }
}
