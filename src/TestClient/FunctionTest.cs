// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
                TestCall1(client);
                TestCall2(client);
                TestFaults(client);
                TestCalbacks(client);
                TestComplexData(client);

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

        private static void TestCall1(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestCall1.Call");

            client.TestCall1(10, "11");

            Console.WriteLine("TestCall1.CallAsync");

            client.TestCall1Async(10, "11").Wait();

            Console.WriteLine("TestCall1.TryCall");

            client.TryTestCall1(10, "11").ThrowIfNotOk();

            Console.WriteLine("TestCall1.TryCallAsync");

            client.TryTestCall1Async(10, "11").Result.ThrowIfNotOk();
        }

        private static void TestCall2(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestCall2.Call");

            var r1 = client.TestCall2(10, "11");
            if (r1 != "123")
                throw new Exception("TestCall2 returned unexpected result!");

            Console.WriteLine("TestCall2.CallAsync");

            var r3 = client.TestCall2Async(10, "11").Result;
            if (r3 != "123")
                throw new Exception("TestCall2Async returned unexpected result!");

            Console.WriteLine("TestCall2.TryCall");

            var r2 = client.TryTestCall2(10, "11");
            r2.ThrowIfNotOk();
            if (r2.Result != "123")
                throw new Exception("TryTestCall2 returned unexpected result!");

            Console.WriteLine("TestCall2.TryCallAsync");

            var r4 = client.TryTestCall2Async(10, "11").Result;
            r4.ThrowIfNotOk();
            if (r4.Result != "123")
                throw new Exception("TestCall2Async returned unexpected result!");
        }

        private static void TestFaults(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestFaults.Regular");

            AssertFault(RpcRetCode.RequestFaulted, "Test exception", () =>
            {
                client.TestRpcException(10, "11");
                return RpcResult.Ok;
            });

            Console.WriteLine("TestFaults.Regular.Try");

            AssertFault(RpcRetCode.RequestFaulted, "Test exception", () => client.TryTestRpcException(10, "11").GetResultInfo());

            Console.WriteLine("TestFaults.Crash");

            AssertFault(RpcRetCode.RequestCrashed, "Request faulted due to", () =>
            {
                client.TestCrash(10, "11");
                return RpcResult.Ok;
            });

            Console.WriteLine("TestFaults.Crash.Try");

            AssertFault(RpcRetCode.RequestCrashed, "Request faulted due to", () => client.TryTestCrash(10, "11").GetResultInfo());

            Console.WriteLine("TestFaults.Custom1");

            AssertCustomFault(RpcRetCode.RequestCrashed, new TestFault1 { Message = "Fault Message 1" }, () =>
            {
                client.TestCallFault(1);
                return RpcResult.Ok;
            });

            Console.WriteLine("TestFaults.Custom1.Try");

            AssertCustomFault(RpcRetCode.RequestCrashed, new TestFault1 { Message = "Fault Message 1" }, () => client.TryTestCallFault(1));
        }

        private static void TestComplexData(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestComplexData.Call");

            var list1 = new List<DateTime>();
            list1.Add(new DateTime(2011, 10, 10));
            list1.Add(new DateTime(2012, 10, 10));

            var list2 = new List<DateTime>();
            list2.Add(new DateTime(2013, 10, 10));
            list2.Add(new DateTime(2014, 10, 10));

            var list3 = new List<DateTime>();
            list3.Add(new DateTime(2015, 10, 10));
            list3.Add(new DateTime(2016, 10, 10));

            var listOfList = new List<List<DateTime>>();
            listOfList.Add(list2);
            listOfList.Add(list3);

            var dictionary = new Dictionary<int, int>();
            dictionary.Add(1, 2);
            dictionary.Add(2, 4);
            dictionary.Add(5, 6);

            var r1 = client.ComplexTypesCall(list1, listOfList, dictionary);

            if (r1.Count != 3)
                throw new Exception("");

            if (r1[0].Item1 != 4023)
                throw new Exception();

            if (r1[1].Item1 != 8058)
                throw new Exception();

            if (r1[2].Item1 != 20)
                throw new Exception();
        }

        private static void AssertFault(RpcRetCode expectedCode, string expectedMessageStart, Func<RpcResult> call)
        {
            string message = null;
            RpcRetCode? code;

            try
            {
                var result = call();
                code = result.Code;
                message = result.Fault.Message;
            }
            catch (AggregateException aex)
            {
                var faultEx = aex.InnerException as RpcFaultException;

                if (faultEx == null)
                    throw new Exception("TestRpcException throws wrong type of exception: " + aex.InnerException.GetType().FullName);

                code = faultEx.ErrorCode;
                message = faultEx.Message;
            }
            catch (Exception ex)
            {
                throw new Exception("TestRpcException throws wrong type of exception: " + ex.GetType().FullName);
            }

            if (code != expectedCode)
                throw new Exception("Invalid return code!");

            if (!message.StartsWith(expectedMessageStart))
                throw new Exception("Invalid exception message!");
        }


        private static void AssertCustomFault<T>(RpcRetCode expectedCode, T expectedFault, Func<RpcResult> call)
            where T : RpcFault
        {
            RpcFault fault;
            RpcRetCode? code;

            try
            {
                var result = call();
                code = result.Code;
                fault = result.Fault;
            }
            catch (AggregateException aex)
            {
                var faultEx = aex.InnerException as RpcFaultException<T>;

                if (faultEx == null)
                    throw new Exception("TestRpcException throws wrong type of exception: " + aex.InnerException.GetType().FullName);

                code = faultEx.ErrorCode;
                fault = faultEx.Fault;
            }
            catch (Exception ex)
            {
                throw new Exception("TestRpcException throws wrong type of exception: " + ex.GetType().FullName);
            }

            if (!(fault is T))
                throw new Exception("Invalid fault data type!");

            if (!fault.Equals(expectedFault))
                throw new Exception("Fault data does not match expected!");
        }

        private static void TestCalbacks(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestCalbacks.Callback1");

            client.InvokeCallbackAsync(1, 10, "11").Wait();

            Console.WriteLine("TestCalbacks.Callback2");

            var r2 = client.InvokeCallbackAsync(2, 10, "11").Result;
            if (r2 != "21")
                throw new Exception("TestCall2Async returned unexpected result!");
        }

        private class CallbackHandler : FunctionTestContract_Gen.CallbackServiceBase
        {
#if NET5_0_OR_GREATER
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
#else
            public override Task TestCallbackNotify1(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.CompletedTask;
            }

            public override Task TestCallback1(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.CompletedTask;
            }

            public override Task<int> TestCallback2(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.FromResult(21);
            }

            public override Task<string> TestCallback3(int p1, string p2)
            {
                throw new Exception("Test Exception");
            }
#endif
        }
    }
}
