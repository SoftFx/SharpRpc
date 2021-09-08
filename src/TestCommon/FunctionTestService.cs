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
using SharpRpc;

namespace TestCommon
{
    public class FunctionTestService : FunctionTestContract_Gen.ServiceBase
    {
#if NET5_0_OR_GREATER
        public override ValueTask TestCall1(int p1, string p2)
#else
        public override Task TestCall1(int p1, string p2)
#endif
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> TestCall2(int p1, string p2)
#else
        public override Task<string> TestCall2(int p1, string p2)
#endif
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return FwAdapter.WrappResult("123");
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> TestCrash(int p1, string p2)
#else
        public override Task<string> TestCrash(int p1, string p2)
#endif
        {
            throw new Exception("This is test unexpected expcetion.");
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> TestRpcException(int p1, string p2)
#else
        public override Task<string> TestRpcException(int p1, string p2)
#endif
        {
            throw new RpcFaultException("Test exception");
        }

#if NET5_0_OR_GREATER
        public override ValueTask TestNotify1(int p1, string p2)
#else
        public override Task TestNotify1(int p1, string p2)
#endif
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask TestCallFault(int faultNo)
#else
        public override Task TestCallFault(int faultNo)
#endif
        {
            if (faultNo == 1)
                throw RpcFaultException.Create("Fault Message 1", new TestFault1 { CustomCode = 11 });
            else
                throw RpcFaultException.Create("Fault Message 2", new TestFault2 { CustomCode = 12 });
        }

#if NET5_0_OR_GREATER
        public async override ValueTask<string> InvokeCallback(int callbackNo, int p1, string p2)
#else
        public async override Task<string> InvokeCallback(int callbackNo, int p1, string p2)
#endif
        {
            if (callbackNo == 1)
                return (await Client.TryAsync.TestCallback1(p1, p2)).Code.ToString();
            else if (callbackNo == 2)
                return (await Client.TryAsync.TestCallback2(p1, p2)).Value.ToString();
            else if (callbackNo == 3)
                return (await Client.TryAsync.TestCallback3(p1, p2)).Value;

            throw new Exception("There is no callabck number " + callbackNo);
        }

#if NET5_0_OR_GREATER
        public override ValueTask<List<Tuple<int>>> ComplexTypesCall(List<DateTime> list, List<List<DateTime>> listOfLists, Dictionary<int, int> dictionary)
#else
        public override Task<List<Tuple<int>>> ComplexTypesCall(List<DateTime> list, List<List<DateTime>> listOfLists, Dictionary<int, int> dictionary)
#endif
        {
            var t1 = list.Sum(d => d.Year);
            var t2 = listOfLists.SelectMany(l => l).Sum(d => d.Year);
            var t3 = dictionary.Values.Sum() + dictionary.Keys.Sum();

            var result = new List<Tuple<int>>();
            result.Add(new Tuple<int>(t1));
            result.Add(new Tuple<int>(t2));
            result.Add(new Tuple<int>(t3));

            return FwAdapter.WrappResult(result);
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<int> TestInStream(StreamReader<int> inputStream, int p1, string p2, StreamTestOptions options)
        {
            var sum = 0;

            await foreach (var i in inputStream)
                sum += i;

            return sum + p1 + int.Parse(p2);
        }
#else
        public override Task<int> TestInStream(StreamReader<int> inputStream, int p1, string p2, StreamTestOptions options)
        {
            return FwAdapter.WrappResult(p1);
        }
#endif

#if NET5_0_OR_GREATER
        public override async ValueTask<int> TestOutStream(StreamWriter<int> outputStream, int p1, string p2, StreamTestOptions options)
#else
        public override async Task<int> TestOutStream(StreamWriter<int> outputStream, int p1, string p2, StreamTestOptions options)
#endif
        {
            for (int i = 1; i <= p1; i++)
                await outputStream.WriteAsync(i);

            if (options == StreamTestOptions.InvokeCompletion)
                await outputStream.CompleteAsync();

            return int.Parse(p2);
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<int> TestDuplexStream(StreamReader<int> inputStream, StreamWriter<int> outputStream, int p1, string p2, StreamTestOptions options)
        {
            await foreach (var item in inputStream)
                await outputStream.WriteAsync(item);

            if (options == StreamTestOptions.InvokeCompletion)
                await outputStream.CompleteAsync();

            return p1 + int.Parse(p2);
        }
#else
        public override Task<int> TestDuplexStream(StreamReader<int> inputStream, StreamWriter<int> outputStream, int p1, string p2, StreamTestOptions options)
        {
            throw new NotImplementedException();
        }
#endif
    }
}
