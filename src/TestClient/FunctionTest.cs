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
using TestCommon;

namespace TestClient
{
    public static class FunctionTest
    {
        public static void Run(string address)
        {
            var endpoint = new TcpClientEndpoint(address, 812, TcpSecurity.None);
            var callback = new CallbackHandler();
            var client = FunctionTestContract_Gen.CreateClient(endpoint, callback);

            try
            {
                Test1(client);
                Test2(client);
                TestCalbacks(client);

                Console.WriteLine("Done testing.");
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aggr && aggr.InnerExceptions.Count == 1)
                    ex = aggr.InnerException;

                Console.WriteLine("Tests failed: " + ex.Message);
            }

            client.Channel.CloseAsync().Wait();
        }

        private static void Test1(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("Test1.Call");

            client.TestCall1(10, "11");

            Console.WriteLine("Test1.CallAsync");

            client.TestCall1Async(10, "11").Wait();

            Console.WriteLine("Test1.TryCall");

            client.TryTestCall1(10, "11").ThrowIfNotOk();

            Console.WriteLine("Test1.TryCallAsync");

            client.TryTestCall1Async(10, "11").Result.ThrowIfNotOk();
        }

        private static void Test2(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("Test2.Call");

            var r1 = client.TestCall2(10, "11");
            if (r1 != "123")
                throw new Exception("TestCall2 returned unexpected result!");

            Console.WriteLine("Test1.CallAsync");

            var r3 = client.TestCall2Async(10, "11").Result;
            if (r3 != "123")
                throw new Exception("TestCall2Async returned unexpected result!");

            Console.WriteLine("Test2.TryCall");

            var r2 = client.TryTestCall2(10, "11");
            r2.ThrowIfNotOk();
            if (r2.Result != "123")
                throw new Exception("TryTestCall2 returned unexpected result!");

            Console.WriteLine("Test2.TryCallAsync");

            var r4 = client.TryTestCall2Async(10, "11").Result;
            r4.ThrowIfNotOk();
            if (r4.Result != "123")
                throw new Exception("TestCall2Async returned unexpected result!");
        }

        private static void TestCalbacks(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("Test2.Callback1");

            client.InvokeCallbackAsync(1, 10, "11").Wait();

            Console.WriteLine("Test2.Callback2");

            var r2 = client.InvokeCallbackAsync(2, 10, "11").Result;
            if (r2 != "21")
                throw new Exception("TestCall2Async returned unexpected result!");
        }

        private class CallbackHandler : FunctionTestContract_Gen.CallbackServiceBase
        {
            public override ValueTask TestCallbackNotify1(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return new ValueTask();
            }

            public override ValueTask TestCallback1(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return new ValueTask();
            }

            public override ValueTask<int> TestCallback2(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return ValueTask.FromResult(21);
            }

            public override ValueTask<string> TestCallback3(int p1, string p2)
            {
                throw new Exception("Test Exception");
            }
        }
    }
}
