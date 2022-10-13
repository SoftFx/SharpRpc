// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestCommon;
using TestCommon.Lib;

namespace TestClient
{
    public static class FunctionTest
    {
        public static void Run(string address, bool ssl)
        {
            Console.WriteLine("Functon test, ssl=" + ssl);
            Console.WriteLine();

            ExecTest<Call1Test>(address, ssl);
            ExecTest<Call2Test>(address, ssl);
            ExecTest<ComplexDataTest>(address, ssl);
            ExecTest<RegularFaultTest>(address, ssl);
            ExecTest<CrashFaultTest>(address, ssl);
            ExecTest<CustomFaultTest>(address, ssl);
            ExecTest<CallbackTest1>(address, ssl);
            ExecTest<CallbackTest2>(address, ssl);
            ExecTest<InputStreamTest>(address, ssl);
            ExecTest<OutputStreamTest>(address, ssl);
            ExecTest<DuplexStreamTest>(address, ssl);
            ExecTest<InputStreamCancellationTest>(address, ssl);
            ExecTest<OutputStreamCancellationTest>(address, ssl);
            //ExecTest<DuplexStreamCancellationTest>(address, ssl);

            if (ssl)
                ExecTest<SessionPropertyTest>(address, ssl);

            Console.WriteLine();
            Console.WriteLine("Done testing.");
        }

        private static void ExecTest<T>(string address, bool ssl)
            where T : TestBase, new()
        {
            var test = new T();
            var cases = test.GetPredefinedCases().ToList();

            Console.WriteLine();

            if (cases.Count > 1)
            {
                Console.WriteLine(test.Name);

                foreach (var tCase in cases)
                {
                    var caseInfoBuilder = new StringBuilder();
                    caseInfoBuilder.Append("\t");
                    tCase.PrintCaseParams(caseInfoBuilder);
                    caseInfoBuilder.Append(" ...");

                    Console.Write(caseInfoBuilder.ToString());
                    ExecTestCase(address, tCase, ssl);
                }
            }
            else
            {
                Console.Write(test.Name + " ...");
                ExecTestCase(address, cases[0], ssl);
            }
        }

        private static void ExecTestCase(string address, TestCase tCase, bool ssl)
        {
            if (RunTestCase(address, tCase, ssl, out var error))
                Console.WriteLine(" Passed");
            else
            {
                Console.WriteLine(" Failed");
                Console.WriteLine();
                Console.WriteLine(error);
                Console.WriteLine();
            }
        }

        private static bool RunTestCase(string address, TestCase tCase, bool ssl, out string errorMessage)
        {
            var security = ssl ? new SslSecurity() : TcpSecurity.None;
            var port = ssl ? 814 : 812;
            var endpoint = new TcpClientEndpoint(address, port, security);

            if (ssl)
                endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            var callback = new CallbackHandler();
            var client = FunctionTestContract_Gen.CreateClient(endpoint, callback);

            var rConnect = client.Channel.TryConnectAsync().Result;

            try
            {
                tCase.RunTest(client);
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aggr && aggr.InnerExceptions.Count == 1)
                    ex = aggr.InnerException;

                errorMessage = ex.Message;
                return false;
            }
            finally
            {
                client.Channel.CloseAsync().Wait();
            }
        }

        private enum CallType
        {
            Regular,
            Try,
            Async,
            TryAsync
        }

        private class Call1Test : TestBase
        {
            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(CallType.Regular);
                yield return CreateCase(CallType.Try);
                yield return CreateCase(CallType.Async);
                yield return CreateCase(CallType.TryAsync);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((CallType)rnd.Next(4));
            }

            private TestCase CreateCase(CallType cType)
            {
                return new TestCase(this)
                    .SetParam("callType", cType);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var type = (CallType)tCase["callType"];

                if (type == CallType.Regular)
                    client.TestCall1(10, "11");
                else if (type == CallType.Async)
                    client.Async.TestCall1(10, "11").Wait();
                else if (type == CallType.Try)
                    client.Try.TestCall1(10, "11").ThrowIfNotOk();
                else if (type == CallType.TryAsync)
                    client.TryAsync.TestCall1(10, "11").Result.ThrowIfNotOk();
            }
        }

        private class Call2Test : TestBase
        {
            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(CallType.Regular);
                yield return CreateCase(CallType.Try);
                yield return CreateCase(CallType.Async);
                yield return CreateCase(CallType.TryAsync);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((CallType)rnd.Next(4));
            }

            private TestCase CreateCase(CallType cType)
            {
                return new TestCase(this)
                    .SetParam("callType", cType);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var type = (CallType)tCase["callType"];

                if (type == CallType.Regular)
                {
                    var r1 = client.TestCall2(10, "11");
                    if (r1 != "123")
                        throw new Exception("TestCall2 returned unexpected result!");
                }
                else if (type == CallType.Try)
                {
                    var r2 = client.Try.TestCall2(10, "11");
                    r2.ThrowIfNotOk();
                    if (r2.Value != "123")
                        throw new Exception("TryTestCall2 returned unexpected result!");
                }
                else if (type == CallType.Async)
                {
                    var r3 = client.Async.TestCall2(10, "11").Result;
                    if (r3 != "123")
                        throw new Exception("TestCall2Async returned unexpected result!");
                }
                else
                {
                    var r4 = client.TryAsync.TestCall2(10, "11").Result;
                    r4.ThrowIfNotOk();
                    if (r4.Value != "123")
                        throw new Exception("TestCall2Async returned unexpected result!");
                }
            }
        }

        private class RegularFaultTest : TestBase
        {
            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(CallType.Regular);
                yield return CreateCase(CallType.Try);
                //yield return CreateCase(CallType.Async);
                //yield return CreateCase(CallType.TryAsync);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((CallType)rnd.Next(2));
            }

            private TestCase CreateCase(CallType cType)
            {
                return new TestCase(this)
                    .SetParam("callType", cType);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var type = (CallType)tCase["callType"];

                AssertFault(RpcRetCode.RequestFault, "Test exception", () =>
                {
                    if (type == CallType.Regular)
                    {
                        client.TestRpcException(10, "11");
                        return RpcResult.Ok;
                    }
                    else //if (type == CallType.Try)
                        return client.Try.TestRpcException(10, "11").GetResultInfo();
                });
            }
        }

        private class CrashFaultTest : TestBase
        {
            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(CallType.Regular);
                yield return CreateCase(CallType.Try);
                //yield return CreateCase(CallType.Async);
                //yield return CreateCase(CallType.TryAsync);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((CallType)rnd.Next(2));
            }

            private TestCase CreateCase(CallType cType)
            {
                return new TestCase(this)
                    .SetParam("callType", cType);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var type = (CallType)tCase["callType"];

                AssertFault(RpcRetCode.RequestCrash, "Request faulted due to", () =>
                {
                    if (type == CallType.Regular)
                    {
                        client.TestCrash(10, "11");
                        return RpcResult.Ok;
                    }
                    else // if(type == CallType.Try)
                        return client.Try.TestCrash(10, "11").GetResultInfo();
                });
            }
        }

        private class CustomFaultTest : TestBase
        {
            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(CallType.Regular);
                yield return CreateCase(CallType.Try);
                //yield return CreateCase(CallType.Async);
                //yield return CreateCase(CallType.TryAsync);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((CallType)rnd.Next(2));
            }

            private TestCase CreateCase(CallType cType)
            {
                return new TestCase(this)
                    .SetParam("callType", cType);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var type = (CallType)tCase["callType"];

                AssertCustomFault(RpcRetCode.RequestCrash, new TestFault1 { CustomCode = 11 }, () =>
                {
                    if (type == CallType.Regular)
                    {
                        client.TestCallFault(1);
                        return RpcResult.Ok;
                    }
                    else
                        return client.Try.TestCallFault(1);
                });
            }
        }

        private class ComplexDataTest : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
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

        private class CallbackTest1 : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var r1 = client.Async.InvokeCallback(1, 10, "11").Result;
                if (r1 != "Ok")
                    throw new Exception("InvokeCallback returned unexpected result!");
            }
        }

        private class CallbackTest2 : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var r2 = client.Async.InvokeCallback(2, 10, "11").Result;
                if (r2 != "21")
                    throw new Exception("InvokeCallback returned unexpected result!");
            }
        }

        private class InputStreamTest : TestBase
        {
            private TestCase CreateCase(ushort windowSize, StreamTestOptions options)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("options", options);
            }

            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(8, StreamTestOptions.JustExit);
                yield return CreateCase(8, StreamTestOptions.ImmediateFault);
                yield return CreateCase(8, StreamTestOptions.ImmediateCustomFault);
                yield return CreateCase(8, StreamTestOptions.None);
                yield return CreateCase(32, StreamTestOptions.None);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((ushort)rnd.Next(8, 100),
                    rnd.Pick(StreamTestOptions.JustExit, StreamTestOptions.ImmediateFault, StreamTestOptions.ImmediateCustomFault));
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var testOptions = (StreamTestOptions)tCase["options"];

                var options = new StreamOptions() { WindowSize = windowSize };

                var call = client.TestInStream(options, TimeSpan.Zero, testOptions);

                var itemsCount = 100;
                var expectedSumm = (1 + itemsCount) * itemsCount / 2;
                var rWrite = RpcResult.Ok;

                for (int i = 1; i <= itemsCount; i++)
                {
                    rWrite = call.InputStream.WriteAsync(i).Result;

                    if (!rWrite.IsOk)
                        break;
                }

                if ((testOptions == StreamTestOptions.None && rWrite.Code != RpcRetCode.Ok)
                        || (testOptions == StreamTestOptions.JustExit && rWrite.Code != RpcRetCode.StreamCompleted)
                        || (testOptions == StreamTestOptions.ImmediateFault && rWrite.Code != RpcRetCode.RequestFault))
                    throw new Exception("WriteAsync() returned " + rWrite.Code);

                var rCompletion = call.InputStream.CompleteAsync().Result;

                if (!rCompletion.IsOk)
                    throw new Exception("CompleteAsync() returned " + rCompletion.Code);

                if (testOptions == StreamTestOptions.ImmediateFault || testOptions == StreamTestOptions.ImmediateCustomFault)
                {
                    var result = call.AsyncResult.Result;

                    if (result.Code != RpcRetCode.RequestFault)
                        throw new Exception("Stream call returned an unexpected result!");
                }
                else
                {
                    var result = call.AsyncResult.Result.Value;

                    if ((testOptions == StreamTestOptions.None && result != expectedSumm)
                        || (testOptions == StreamTestOptions.JustExit && result != -2))
                        throw new Exception("Stream call returned an unexpected result!");
                }
            }
        }

        private class OutputStreamTest : TestBase
        {
            private TestCase CreateCase(ushort windowSize, bool withCompletion)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("withCompletion", withCompletion);
            }

            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(8, true);
                yield return CreateCase(8, false);
                yield return CreateCase(32, true);
                yield return CreateCase(32, false);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((ushort)rnd.Next(8, 100), rnd.Next(2) > 0);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var withCompletion = (bool)tCase["withCompletion"];

                var itemsCount = 100;
                var expectedSumm = (1 + itemsCount) * itemsCount / 2;

                var options = withCompletion ? StreamTestOptions.InvokeCompletion : StreamTestOptions.DoNotInvokeCompletion;
                var streamOptions = new StreamOptions() { WindowSize = windowSize };
                var call = client.TestOutStream(streamOptions, TimeSpan.Zero, itemsCount, options);

                var e = call.OutputStream.GetEnumerator();
                var summ = 0;

                while (e.MoveNextAsync().Result)
                    summ += e.Current;

                if (summ != expectedSumm)
                    throw new Exception("Items summ does not match expected value!");

                var ret = call.AsyncResult.Result;

                if (ret.Value != 0)
                    throw new Exception("Returned value does not match expected!");
            }
        }

        private class DuplexStreamTest : TestBase
        {
            private TestCase CreateCase(ushort windowSize, bool withCompletion)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("withCompletion", withCompletion);
            }

            public override IEnumerable<TestCase> GetPredefinedCases()
            {
                yield return CreateCase(8, true);
                yield return CreateCase(8, false);
                yield return CreateCase(32, true);
                yield return CreateCase(32, false);
            }

            public override TestCase GetRandomCase(Random rnd)
            {
                return CreateCase((ushort)rnd.Next(8, 100), rnd.Next(2) > 0);
            }

            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var withCompletion = (bool)tCase["withCompletion"];

                var options = withCompletion ? StreamTestOptions.InvokeCompletion : StreamTestOptions.DoNotInvokeCompletion;
                var streamOptions = new DuplexStreamOptions() { InputWindowSize = windowSize, OutputWindowSize = windowSize };
                var call = client.TestDuplexStream(streamOptions, TimeSpan.Zero, options);

                var itemsCount = 100;
                var expectedSumm = (1 + itemsCount) * itemsCount / 2;

                var readTask = Task.Factory.StartNew<int>(() =>
                {
                    var e = call.OutputStream.GetEnumerator();
                    var sm = 0;

                    while (e.MoveNextAsync().Result)
                        sm += e.Current;

                    return sm;
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

                if (result != 0)
                    throw new Exception("Stream call returned an unexpected result!");
            }
        }

        private class TestCallCancellation_CancelAfterDelay : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var cancelSrc = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

                var result = client.CancellableCall(TimeSpan.FromMinutes(5), cancelSrc.Token);

                if (!result)
                    throw new Exception("CancellableCall() returned an unexpected result!");
            }
        }

        private class TestCallCancellation_CancelBefore : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var cancelSrc = new CancellationTokenSource();
                cancelSrc.Cancel();

                var result = client.CancellableCall(TimeSpan.FromMinutes(2), cancelSrc.Token);

                if (!result)
                    throw new Exception("CancellableCall() returned an unexpected result!");
            }
        }

        private class InputStreamCancellationTest : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var cancelSrc2 = new CancellationTokenSource(500);
                var options = new StreamOptions();
                var callObj2 = client.TestInStream(options, TimeSpan.FromMilliseconds(100), StreamTestOptions.InvokeCompletion);

                callObj2.InputStream.EnableCancellation(cancelSrc2.Token);

                var expectedSum = 0;

                for (int i = 0; i < 1000; i++)
                {
                    var wResult = callObj2.InputStream.WriteAsync(i).Result;
                    if (!wResult.IsOk)
                        break;

                    expectedSum += i;
                }

                var retVal2 = callObj2.AsyncResult.Result.Value;

                if (retVal2 != expectedSum)
                    throw new Exception("Stream call returned an unexpected result! Expected " + expectedSum + ", returned " + retVal2);
            }
        }

        private class OutputStreamCancellationTest : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var cancelSrc = new CancellationTokenSource(500);
                var options = new StreamOptions() { WindowSize = 10 };
                var callObj = client.TestOutStream(options, TimeSpan.FromMilliseconds(100), 100, StreamTestOptions.InvokeCompletion);

                var e = callObj.OutputStream.GetEnumerator(cancelSrc.Token);

                while (true)
                {
                    if (!e.MoveNextAsync().Result)
                        break;
                }

                var retVal = callObj.AsyncResult.Result.Value;

                if (retVal != -1)
                    throw new Exception("Stream call returned an unexpected result!");
            }
        }

        //private class DuplexStreamCancellationTest : TestBase
        //{
        //    public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
        //    {
        //        var cancelSrc3 = new CancellationTokenSource(500);
        //        var duplexOptions = new DuplexStreamOptions() { InputWindowSize = 10, OutputWindowSize = 10 };
        //        var callObj3 = client.TestDuplexStream(duplexOptions, TimeSpan.FromMilliseconds(100), StreamTestOptions.InvokeCompletion);

        //        var retVal3 = callObj3.AsyncResult.Result.Value;

        //        if (retVal3 != -1)
        //            throw new Exception("Stream call returned an unexpected result!");
        //    }
        //}

        private class SessionPropertyTest : TestBase
        {
            public override void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client)
            {
                var userProp = client.GetSessionSharedProperty("UserName");
                var tokenProp = client.GetSessionSharedProperty("AuthToken");

                if (userProp != "Admin")
                    throw new Exception("UserName property does not match!");

                if (tokenProp != "15")
                    throw new Exception("AuthToken property does not match!");
            }
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

            public override ValueTask TestCallback1(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return new ValueTask();
            }

            public override ValueTask<int> TestCallback2(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return ValueTask.FromResult(21);
            }

            public override ValueTask<string> TestCallback3(CallContext context, int p1, string p2)
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

            public override Task TestCallback1(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.CompletedTask;
            }

            public override Task<int> TestCallback2(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.FromResult(21);
            }

            public override Task<string> TestCallback3(CallContext context, int p1, string p2)
            {
                throw new Exception("Test Exception");
            }
#endif
        }
    }
}
