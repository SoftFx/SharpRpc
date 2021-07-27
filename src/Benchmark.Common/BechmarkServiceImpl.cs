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

namespace Benchmark.Common
{
    public class BechmarkServiceImpl : BenchmarkContract_Gen.Service
    {
        public override ValueTask SendUpdate(FooEntity entity)
        {
            return new ValueTask();
        }

        public override ValueTask SendUpdate2(FooEntity entity)
        {
            return new ValueTask();
        }

        public override ValueTask DummyMethod1(int p1, string p2)
        {
            return new ValueTask();
        }

        public override ValueTask<string> DummyMethod2(int p1, string p2)
        {
            return ValueTask.FromResult("");
        }
    }
}
