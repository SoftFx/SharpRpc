// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

                TestInputStream(client);
                TestOutputStream(client, true);
                TestOutputStream(client, false);
                TestDuplexStream(client, true);
                TestDuplexStream(client, false);

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

            client.Async.TestCall1(10, "11").Wait();

            Console.WriteLine("TestCall1.TryCall");

            client.Try.TestCall1(10, "11").ThrowIfNotOk();

            Console.WriteLine("TestCall1.TryCallAsync");

            client.TryAsync.TestCall1(10, "11").Result.ThrowIfNotOk();
        }

        private static void TestCall2(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestCall2.Call");

            var r1 = client.TestCall2(10, "11");
            if (r1 != "123")
                throw new Exception("TestCall2 returned unexpected result!");

            Console.WriteLine("TestCall2.CallAsync");

            var r3 = client.Async.TestCall2(10, "11").Result;
            if (r3 != "123")
                throw new Exception("TestCall2Async returned unexpected result!");

            Console.WriteLine("TestCall2.TryCall");

            var r2 = client.Try.TestCall2(10, "11");
            r2.ThrowIfNotOk();
            if (r2.Value != "123")
                throw new Exception("TryTestCall2 returned unexpected result!");

            Console.WriteLine("TestCall2.TryCallAsync");

            var r4 = client.TryAsync.TestCall2(10, "11").Result;
            r4.ThrowIfNotOk();
            if (r4.Value != "123")
                throw new Exception("TestCall2Async returned unexpected result!");
        }

        private static void TestFaults(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestFaults.Regular");

            AssertFault(RpcRetCode.RequestFault, "Test exception", () =>
            {
                client.TestRpcException(10, "11");
                return RpcResult.Ok;
            });

            Console.WriteLine("TestFaults.Regular.Try");

            AssertFault(RpcRetCode.RequestFault, "Test exception", () => client.Try.TestRpcException(10, "11").GetResultInfo());

            Console.WriteLine("TestFaults.Crash");

            AssertFault(RpcRetCode.RequestCrash, "Request faulted due to", () =>
            {
                client.TestCrash(10, "11");
                return RpcResult.Ok;
            });

            Console.WriteLine("TestFaults.Crash.Try");

            AssertFault(RpcRetCode.RequestCrash, "Request faulted due to", () => client.Try.TestCrash(10, "11").GetResultInfo());

            Console.WriteLine("TestFaults.Custom1");

            AssertCustomFault(RpcRetCode.RequestCrash, new TestFault1 { CustomCode = 11 }, () =>
            {
                client.TestCallFault(1);
                return RpcResult.Ok;
            });

            Console.WriteLine("TestFaults.Custom1.Try");

            AssertCustomFault(RpcRetCode.RequestCrash, new TestFault1 { CustomCode = 11 }, () => client.Try.TestCallFault(1));
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
                message = result.FaultMessage;
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

            if (string.IsNullOrEmpty(message) || !message.StartsWith(expectedMessageStart))
                throw new Exception("Invalid exception message!");
        }

        private static void AssertCustomFault<T>(RpcRetCode expectedCode, T expectedFault, Func<RpcResult> call)
        {
            object fault;
            RpcRetCode? code;

            try
            {
                var result = call();
                code = result.Code;
                fault = result.CustomFaultData;
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

            var r1 = client.Async.InvokeCallback(1, 10, "11").Result;
            if (r1 != "Ok")
                throw new Exception("InvokeCallback returned unexpected result!");

            Console.WriteLine("TestCalbacks.Callback2");

            var r2 = client.Async.InvokeCallback(2, 10, "11").Result;
            if (r2 != "21")
                throw new Exception("InvokeCallback returned unexpected result!");
        }

        private static void TestInputStream(FunctionTestContract_Gen.Client client)
        {
            Console.WriteLine("TestStreams.Input");

            var call = client.TestInStream(10, "11", StreamTestOptions.DoNotInvokeCompletion);

            var itemsCount = 100;
            var expectedSumm = (1 + itemsCount) * itemsCount / 2;
            var expectedResult = expectedSumm + 10 + 11;

            for (int i = 1; i <= itemsCount; i++)
            {
                var rWrite = call.InputStream.WriteAsync(i).Result;
                if (!rWrite.IsOk)
                    throw new Exception("WriteAsync() returned " + rWrite.Code);
            }

            var rCompletion = call.InputStream.CompleteAsync().Result;

            if (!rCompletion.IsOk)
                throw new Exception("CompleteAsync() returned " + rCompletion.Code);

            var result = call.AsyncResult.Result.Value;

            if (result != expectedResult)
                throw new Exception("Stream call returned an unexpected result!");
        }

        private static void TestOutputStream(FunctionTestContract_Gen.Client client, bool withCompletion)
        {
            Console.WriteLine("TestStreams.Output, completion=" + withCompletion);

#if NET5_0_OR_GREATER
            var itemsCount = 100;
            var expectedSumm = (1 + itemsCount) * itemsCount / 2;
            var expectedResult = 11;

            var options = withCompletion ? StreamTestOptions.InvokeCompletion : StreamTestOptions.DoNotInvokeCompletion;
            var call = client.TestOutStream(itemsCount, "11", options);

            var e = call.OutputStream.GetAsyncEnumerator();
            var summ = 0;

            while (e.MoveNextAsync().Result)
                summ += e.Current;

            if (summ != expectedSumm)
                throw new Exception("Items summ does not match expected value!");

            var ret = call.AsyncResult.Result;

            if (ret.Value != expectedResult)
                throw new Exception("Returned value does not match expected!");
#endif
        }

        private static void TestDuplexStream(FunctionTestContract_Gen.Client client, bool withCompletion)
        {
            Console.WriteLine("TestStreams.Duplex, completion=" + withCompletion);

#if NET5_0_OR_GREATER

            var options = withCompletion ? StreamTestOptions.InvokeCompletion : StreamTestOptions.DoNotInvokeCompletion;
            var call = client.TestDuplexStream(10, "11", options);

            var itemsCount = 100;
            var expectedSumm = (1 + itemsCount) * itemsCount / 2;
            var expectedResult = 10 + 11;

            var readTask = Task.Factory.StartNew<int>(() =>
            {
                var e = call.OutputStream.GetAsyncEnumerator();
                var summ = 0;

                while (e.MoveNextAsync().Result)
                    summ += e.Current;

                return summ;
            });

            for (int i = 1; i <= itemsCount; i++)
            {
                var rWrite = call.InputStream.WriteAsync(i).Result;
                if (!rWrite.IsOk)
                    throw new Exception("WriteAsync() returned " + rWrite.Code);
            }

            var rCompletion = call.InputStream.CompleteAsync().Result;

            if (!rCompletion.IsOk)
                throw new Exception("CompleteAsync() returned " + rCompletion.Code);

            var summ = readTask.Result;
            
            if (summ != expectedSumm)
                throw new Exception("Items summ does not match expected value!");

            var result = call.AsyncResult.Result.Value;

            if (result != expectedResult)
                throw new Exception("Stream call returned an unexpected result!");
#endif
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
