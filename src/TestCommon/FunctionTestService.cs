// Copyright © 2021 Soft-Fx. All rights reserved.
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
using SharpRpc;

namespace TestCommon
{
    public class FunctionTestService : FunctionTestContract_Gen.Service
    {
        public override ValueTask TestCall1(int p1, string p2)
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return new ValueTask();
        }

        public override ValueTask<string> TestCall2(int p1, string p2)
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return ValueTask.FromResult("123");
        }

        public override ValueTask<string> TestCall3(int p1, string p2)
        {
            throw new Exception("Test Exception");
        }

        public override ValueTask TestNotify1(int p1, string p2)
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return new ValueTask();
        }
    }
}
