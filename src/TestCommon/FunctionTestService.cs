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
    public class FunctionTestService : FunctionTestContract_Gen.ServiceBase
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

        public override ValueTask<string> TestCrash(int p1, string p2)
        {
            throw new Exception("This is test unexpected expcetion.");
        }

        public override ValueTask<string> TestRpcException(int p1, string p2)
        {
            throw new RpcFaultException("Test exception");
        }

        public override ValueTask TestNotify1(int p1, string p2)
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return new ValueTask();
        }

        public override ValueTask TestCallFault(int faultNo)
        {
            if (faultNo == 1)
                throw RpcFaultException.Create(new TestFault1 { Message = "Fault Message 1" });
            else
                throw RpcFaultException.Create(new TestFault2 { Message = "Fault Message 2" });
        }

        public async override ValueTask<string> InvokeCallback(int callbackNo, int p1, string p2)
        {
            if (callbackNo == 1)
            {
                await Client.TestCallback1Async(p1, p2);
                return "void";
            }
            else if (callbackNo == 2)
                return (await Client.TestCallback2Async(p1, p2)).ToString();
            else if (callbackNo == 3)
                return await Client.TestCallback3Async(p1, p2);

            throw new Exception("There is no callabck number " + callbackNo);
        }

        public override ValueTask<List<Tuple<int>>> ComplexTypesCall(List<DateTime> list, List<List<DateTime>> listOfLists, Dictionary<int, int> dictionary)
        {
            var t1 = list.Sum(d => d.Year);
            var t2 = listOfLists.SelectMany(l => l).Sum(d => d.Year);
            var t3 = dictionary.Values.Sum() + dictionary.Keys.Sum();

            var result = new List<Tuple<int>>();
            result.Add(new Tuple<int>(t1));
            result.Add(new Tuple<int>(t2));
            result.Add(new Tuple<int>(t3));
            return ValueTask.FromResult(result);
        }
    }
}
