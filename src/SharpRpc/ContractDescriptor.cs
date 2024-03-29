﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class ContractDescriptor
    {
        public ContractDescriptor(IRpcSerializer serializer, IMessageFactory factory)
        {
            SerializationAdapter = serializer;
            SystemMessages = factory;
        }

        public IRpcSerializer SerializationAdapter { get; }
        public IMessageFactory SystemMessages { get; }
    }

    public class ServiceDescriptor : ContractDescriptor
    {
        public ServiceDescriptor(ContractDescriptor contract, Func<RpcCallHandler> implFactory)
            : base(contract.SerializationAdapter, contract.SystemMessages)
        {
            Contract = contract;
            ServiceImplFactory = implFactory;
        }

        public ContractDescriptor Contract { get; }
        public Func<RpcCallHandler> ServiceImplFactory { get; }
    }
}
