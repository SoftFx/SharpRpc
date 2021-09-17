// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCommon
{
    public class BenchmarkClient
    {
        private readonly CallbackService _callback = new CallbackService();

        public BenchmarkClient(string address, int port, TcpSecurity security)
        {
            var endpoint = new TcpClientEndpoint(address, port, security);
            endpoint.Credentials = new BasicCredentials("Admin", "zzzz");
            BenchmarkContractCfg.ConfigureEndpoint(endpoint);
            Stub = BenchmarkContract_Gen.CreateClient(endpoint, _callback);
        }

        public BenchmarkContract_Gen.Client Stub { get; }
        public Channel Channel => Stub.Channel;

        private class CallbackService : BenchmarkContract_Gen.CallbackServiceBase
        {
            private int _msgCounter;

#if NET5_0_OR_GREATER
            public override ValueTask ApplyUpdateOnClient(CallContext context, FooEntity entity)
#else
            public override Task ApplyUpdateOnClient(CallContext context, FooEntity entity)
#endif
            {
                _msgCounter++;
                return FwAdapter.AsyncVoid;
            }

#if NET5_0_OR_GREATER
            public override ValueTask SendUpdateToClient(FooEntity entity)
#else
            public override Task SendUpdateToClient(FooEntity entity)
#endif
            {
                //_msgCounter++;
                return FwAdapter.AsyncVoid;
            }
        }
    }
}
