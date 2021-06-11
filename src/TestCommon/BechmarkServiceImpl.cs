// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestCommon
{
    public class BechmarkServiceImpl : BenchmarkContract_Gen.ServiceBase
    {
#if NET5_0_OR_GREATER
        public override ValueTask SendUpdate(FooEntity entity)
#else
        public override Task SendUpdate(FooEntity entity)
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask ApplyUpdate(FooEntity entity)
#else
        public override Task ApplyUpdate(FooEntity entity)
#endif
        {
            return FwAdapter.AsyncVoid;
        }
    }
}
