// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using SharpRpc.Streaming;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestClient.TestLib;
using TestCommon;
using TestCommon.Lib;

namespace TestClient
{
    public static class FunctionTest
    {
        public static void Run(string address)
        {
            var runner = new TestRunner();
            AddCallCases(runner, address, false, out var client);
            AddCallCases(runner, address, true, out var sslClient);
            runner.RunAll();

            client.Channel.CloseAsync().Wait();
            sslClient.Channel.CloseAsync().Wait();
        }

        private static void AddCallCases(TestRunner runner, string address, bool ssl, out FunctionTestContract_Gen.Client client)
        {
            client = CreateClient(address, ssl);
            var clientName = ssl ? "SSL" : "Unsecured";

            var rConnect = client.Channel.TryConnectAsync().Result;

            runner.AddCases(new Call1Test().GetCases(clientName, client));
            runner.AddCases(new Call2Test().GetCases(clientName, client));
            runner.AddCases(new BigObjectTest().GetCases(clientName, client));
            runner.AddCases(new BigStringTest().GetCases(clientName, client));
            runner.AddCases(new ComplexDataTest().GetCases(clientName, client));
            runner.AddCases(new RegularFaultTest().GetCases(clientName, client));
            runner.AddCases(new CrashFaultTest().GetCases(clientName, client));
            runner.AddCases(new CustomFaultTest().GetCases(clientName, client));
            runner.AddCases(new CallbackTest1().GetCases(clientName, client));
            runner.AddCases(new CallbackTest2().GetCases(clientName, client));
            runner.AddCases(new InputStreamTest().GetCases(clientName, client));
            runner.AddCases(new OutputStreamTest().GetCases(clientName, client));
            runner.AddCases(new DuplexStreamTest().GetCases(clientName, client));
            runner.AddCases(new OutputBinStreamTest().GetCases(clientName, client));
            runner.AddCases(new InputStreamCancellationTest().GetCases(clientName, client));
            runner.AddCases(new OutputStreamCancellationTest().GetCases(clientName, client));

            runner.AddCases(new SessionDropByServerTest(address, ssl).GetCases(clientName));
            runner.AddCases(new ConnectActionAbortTest(address, ssl).GetCases(clientName));
        }

        private static FunctionTestContract_Gen.Client CreateClient(string address, bool ssl)
        {
            var security = ssl ? new SslSecurity(NullCertValidator) : TcpSecurity.None;
            var port = 812;
            var serviceName = ssl ? "func/ssl" : "func";
            var endpoint = new TcpClientEndpoint(new DnsEndPoint(address, port), serviceName, security);

            if (ssl)
                endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            //endpoint.Logger = new ConsoleLogger() { IsVerboseEnabled = true, IsMessageLoggingEnabled = true, IsAuxMessageLoggingEnabled = true };

            var callback = new CallbackHandler();
            return FunctionTestContract_Gen.CreateClient(endpoint, callback);
        }

        private static bool NullCertValidator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        //private static void ExecTest<T>(string address, bool ssl)
        //    where T : TestBase, new()
        //{
        //    var test = new T();
        //    var cases = test.GetPredefinedCases().ToList();

        //    Console.WriteLine();

        //    if (cases.Count > 1)
        //    {
        //        Console.WriteLine(test.Name);

        //        foreach (var tCase in cases)
        //        {
        //            var caseInfoBuilder = new StringBuilder();
        //            caseInfoBuilder.Append("\t");
        //            tCase.PrintCaseParams(caseInfoBuilder);
        //            caseInfoBuilder.Append(" ...");

        //            Console.Write(caseInfoBuilder.ToString());
        //            ExecTestCase(address, tCase, ssl);
        //        }
        //    }
        //    else
        //    {
        //        Console.Write(test.Name + " ...");
        //        ExecTestCase(address, cases[0], ssl);
        //    }
        //}

        //private static void ExecTestCase(string address, TestCase tCase, bool ssl)
        //{
        //    if (RunTestCase(address, tCase, ssl, out var error))
        //        Console.WriteLine(" Passed");
        //    else
        //    {
        //        Console.WriteLine(" Failed");
        //        Console.WriteLine();
        //        Console.WriteLine(error);
        //        Console.WriteLine();
        //    }
        //}

        //private static bool RunTestCase(string address, TestCase tCase, bool ssl, out string errorMessage)
        //{
        //    var security = ssl ? new SslSecurity(TestBase.NullCertValidator) : TcpSecurity.None;
        //    var port = 812;
        //    var serviceName = ssl ? "func/ssl" : "func";
        //    var endpoint = new TcpClientEndpoint(new DnsEndPoint(address, port), serviceName, security);

        //    if (ssl)
        //        endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

        //    var callback = new CallbackHandler();
        //    var client = FunctionTestContract_Gen.CreateClient(endpoint, callback);

        //    var rConnect = client.Channel.TryConnectAsync().Result;

        //    try
        //    {
        //        tCase.RunTest(client);
        //        errorMessage = null;
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        if (ex is AggregateException aggr && aggr.InnerExceptions.Count == 1)
        //            ex = aggr.InnerException;

        //        errorMessage = ex.Message;
        //        return false;
        //    }
        //    finally
        //    {
        //        client.Channel.CloseAsync().Wait();
        //    }
        //}

        private enum CallType
        {
            Regular,
            Try,
            Async,
            TryAsync
        }

        private class Call1Test : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(CallType.Regular, clientDescription, client);
                yield return CreateCase(CallType.Try, clientDescription, client);
                yield return CreateCase(CallType.Async, clientDescription, client);
                yield return CreateCase(CallType.TryAsync, clientDescription, client);
            }

            private TestCase CreateCase(CallType cType, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("callType", cType.ToString(), cType)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var type = (CallType)tCase["callType"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

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
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(CallType.Regular, clientDescription, client);
                yield return CreateCase(CallType.Try, clientDescription, client);
                yield return CreateCase(CallType.Async, clientDescription, client);
                yield return CreateCase(CallType.TryAsync, clientDescription, client);
            }

            private TestCase CreateCase(CallType cType, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("callType", cType)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var type = (CallType)tCase["callType"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

                if (type == CallType.Regular)
                {
                    var r1 = client.TestCall2(10, "11");
                    if (r1 != "123")
                        throw new Exception("TestCall2 returned unexpected result!");
                }
                else if (type == CallType.Try)
                {
                    var r2 = client.Try.TestCall2(10, "11").GetValueOrThrow();
                    if (r2 != "123")
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
                    var r4 = client.TryAsync.TestCall2(10, "11").Result.GetValueOrThrow();
                    if (r4 != "123")
                        throw new Exception("TestCall2Async returned unexpected result!");
                }
            }
        }

        private class RegularFaultTest : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(CallType.Regular, clientDescription, client);
                yield return CreateCase(CallType.Try, clientDescription, client);
                //yield return CreateCase(CallType.Async);
                //yield return CreateCase(CallType.TryAsync);
            }

            private TestCase CreateCase(CallType cType, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("callType", cType)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var type = (CallType)tCase["callType"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

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
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(CallType.Regular, clientDescription, client);
                yield return CreateCase(CallType.Try, clientDescription, client);
                //yield return CreateCase(CallType.Async);
                //yield return CreateCase(CallType.TryAsync);
            }

            private TestCase CreateCase(CallType cType, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("callType", cType)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var type = (CallType)tCase["callType"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

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
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(CallType.Regular, clientDescription, client);
                yield return CreateCase(CallType.Try, clientDescription, client);
                //yield return CreateCase(CallType.Async);
                //yield return CreateCase(CallType.TryAsync);
            }

            private TestCase CreateCase(CallType cType, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("callType", cType)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var type = (CallType)tCase["callType"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

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
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

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

        private class BigObjectTest : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var data = new FooData();
                data.Name = "1231239432943214812[812[3812[38912-3[12391283-01823-128423-1411-20481-238012-3812-381230812";
                data.Relatives = new List<FooData>();

                for (int i = 0; i < 1000000; i++)
                    data.Relatives.Add(new FooData() { Name = data.Name });

                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                client.TestCall3(data);
            }
        }

        private class BigStringTest : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < 10000; i++)
                    builder.Append("1234567890_");

                var data = new FooData();
                data.Name = builder.ToString();
                data.Relatives = new List<FooData>();

                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var resp = client.TestCall3(data);

                if (resp != data.Name)
                    throw new Exception("The result string does not match the input string!");
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
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var r1 = client.Async.InvokeCallback(1, 10, "11").Result;
                if (r1 != "Ok")
                    throw new Exception("InvokeCallback returned unexpected result!");
            }
        }

        private class CallbackTest2 : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var r2 = client.Async.InvokeCallback(2, 10, "11").Result;
                if (r2 != "21")
                    throw new Exception("InvokeCallback returned unexpected result!");
            }
        }

        private class InputStreamTest : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(8, StreamTestOptions.JustExit, clientDescription, client);
                yield return CreateCase(8, StreamTestOptions.ImmediateFault, clientDescription, client);
                yield return CreateCase(8, StreamTestOptions.ImmediateCustomFault, clientDescription, client);
                yield return CreateCase(8, StreamTestOptions.None, clientDescription, client);
                yield return CreateCase(32, StreamTestOptions.None, clientDescription, client);
            }

            private TestCase CreateCase(ushort windowSize, StreamTestOptions options,
                string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("options", options)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var testOptions = (StreamTestOptions)tCase["options"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

                var options = new StreamOptions() { WindowSize = windowSize };

                var call = client.TestInStream(options, TimeSpan.Zero, testOptions);

                var itemsCount = 1000;
                var expectedSumm = (1 + itemsCount) * itemsCount / 2;
                var rWrite = RpcResult.Ok;

                for (int i = 1; i <= itemsCount; i++)
                {
                    rWrite = call.InputStream.WriteAsync(i).Result;

                    if (!rWrite.IsOk)
                        break;
                }

                if ((testOptions == StreamTestOptions.None && rWrite.Code != RpcRetCode.Ok)
                        || (testOptions == StreamTestOptions.JustExit && rWrite.Code != RpcRetCode.OperationCanceled)
                        || (testOptions == StreamTestOptions.ImmediateFault && rWrite.Code != RpcRetCode.OperationCanceled))
                    throw new Exception("WriteAsync() returned " + rWrite.Code + "!");

                var rCompletion = call.InputStream.CompleteAsync().Result;

                if (!rCompletion.IsOk)
                    throw new Exception("CompleteAsync() returned " + rCompletion.Code);

                if (testOptions == StreamTestOptions.ImmediateFault || testOptions == StreamTestOptions.ImmediateCustomFault)
                {
                    var result = call.AsyncResult.Result;

                    if (result.Code != RpcRetCode.RequestFault)
                        throw new Exception(result.FaultMessage);
                }
                else
                {
                    var result = call.AsyncResult.Result.Value;

                    if (testOptions == StreamTestOptions.JustExit && result.ExitCode != StreamCallExitCode.ImmediateExit)
                        throw new Exception("Exit code does not match expected!");

                    if (testOptions == StreamTestOptions.None)
                    {
                        if (result.ItemSum != expectedSumm)
                            throw new Exception("Returned sum does not macth!");

                        if (result.ExitCode != StreamCallExitCode.StreamCompleted)
                            throw new Exception("Exit code does not match expected!");
                    }
                }
            }
        }

        private class OutputStreamTest : TestBase
        {
            private TestCase CreateCase(ushort windowSize, bool withCompletion, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("withCompletion", withCompletion)
                    .SetParam("client", clientDescription, client);
            }

            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(8, true, clientDescription, client);
                yield return CreateCase(8, false, clientDescription, client);
                yield return CreateCase(32, true, clientDescription, client);
                yield return CreateCase(32, false, clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var withCompletion = (bool)tCase["withCompletion"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

                var itemsCount = 100;
                var expectedSumm = (1 + itemsCount) * itemsCount / 2;

                var options = withCompletion ? StreamTestOptions.InvokeCompletion : StreamTestOptions.DoNotInvokeCompletion;
                var streamOptions = new StreamOptions() { WindowSize = windowSize };
                var call = client.TestOutStream(streamOptions, TimeSpan.Zero, itemsCount, options);

                var summ = 0;

                using (var e = call.OutputStream.GetEnumerator())
                {
                    while (e.MoveNextAsync().Result)
                        summ += e.Current;
                }

                if (summ != expectedSumm)
                    throw new Exception("Items summ does not match expected value!");

                var ret = call.AsyncResult.Result;

                if (ret.Value.ExitCode != StreamCallExitCode.StreamCompleted)
                    throw new Exception("Exit code does not match expected!");
            }
        }

        private class OutputBinStreamTest : TestBase
        {
            private TestCase CreateCase(ushort windowSize, StreamWriteOptions writeMode, StreamReadOptions readMode,
                string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("readMode", readMode)
                    .SetParam("writeMode", writeMode)
                    .SetParam("client", clientDescription, client);
            }

            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                foreach (var testCase in GetCases(38, clientDescription, client))
                    yield return testCase;

                foreach (var testCase in GetCases(54363, clientDescription, client))
                    yield return testCase;
            }

            private IEnumerable<TestCase> GetCases(ushort windowSize, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(windowSize, StreamWriteOptions.OneByOne, StreamReadOptions.OneByOne, clientDescription, client);
                yield return CreateCase(windowSize, StreamWriteOptions.Bulk, StreamReadOptions.OneByOne, clientDescription, client);
                yield return CreateCase(windowSize, StreamWriteOptions.BulkStartCommit, StreamReadOptions.OneByOne, clientDescription, client);

                yield return CreateCase(windowSize, StreamWriteOptions.OneByOne, StreamReadOptions.OneByOne, clientDescription, client);
                yield return CreateCase(windowSize, StreamWriteOptions.OneByOne, StreamReadOptions.Paged, clientDescription, client);
                yield return CreateCase(windowSize, StreamWriteOptions.OneByOne, StreamReadOptions.Bulk, clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var wirteMode = (StreamWriteOptions)tCase["writeMode"];
                var readMode = (StreamReadOptions)tCase["readMode"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

                var fileToDownstream = "19084.jpg"; // Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestClient.exe");
                var expectedCrc = CalcFileCrc(fileToDownstream);
                var streamOptions = new StreamOptions() { WindowSize = windowSize };
                var call = client.TestOutBinStream(streamOptions, fileToDownstream, StreamTestOptions.None, wirteMode);
                var crc = 0;

                if (readMode == StreamReadOptions.Paged)
                {
                    using (var memStream = new MemoryStream())
                    {
                        call.OutputStream.ReadAllAsync(memStream).Wait();
                        memStream.Position = 0;
                        crc = CalcFileCrc(memStream);
                    }
                }
                else if (readMode == StreamReadOptions.OneByOne)
                {
                    var e = call.OutputStream.GetEnumerator();
                    while (e.MoveNextAsync().Result)
                        crc += e.Current;
                }
                else if (readMode == StreamReadOptions.Bulk)
                {
                    var e = call.OutputStream.GetBulkEnumerator();
                    var buffer = new byte[4096];

                    while (true)
                    {
                        var result = e.Read(new ArraySegment<byte>(buffer)).Result;
                        var bytesRed = result.GetValueOrThrow();

                        if (bytesRed == 0)
                            break;

                        for (int i = 0; i < bytesRed; i++)
                            crc += buffer[i];
                    }
                }

                if (crc != expectedCrc)
                    throw new Exception("Items summ does not match expected value!");

                var ret = call.AsyncResult.Result;

                if (ret.Value.ExitCode != StreamCallExitCode.StreamCompleted)
                    throw new Exception("Exit code does not match expected!");
            }

            private int CalcFileCrc(string filePath)
            {
                using (var file = File.OpenRead(filePath))
                    return CalcFileCrc(file);
            }

            private int CalcFileCrc(Stream file)
            {
                var buffer = new byte[4096];
                int crc = 0;

                while (true)
                {
                    var bytes = file.Read(buffer, 0, buffer.Length);

                    if (bytes == 0)
                        break;

                    for (var i = 0; i < bytes; i++)
                        crc += buffer[i];
                }

                return crc;
            }
        }

        private class DuplexStreamTest : TestBase
        {
            private TestCase CreateCase(ushort windowSize, bool withCompletion, string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("windowSize", windowSize)
                    .SetParam("withCompletion", withCompletion)
                    .SetParam("client", clientDescription, client);
            }

            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(8, true, clientDescription, client);
                yield return CreateCase(8, false, clientDescription, client);
                yield return CreateCase(32, true, clientDescription, client);
                yield return CreateCase(32, false, clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var windowSize = (ushort)tCase["windowSize"];
                var withCompletion = (bool)tCase["withCompletion"];
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");

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
            public override void RunTest(TestCase tCase)
            {
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var cancelSrc = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                var result = client.CancellableCall(TimeSpan.FromMinutes(5), cancelSrc.Token);

                if (!result)
                    throw new Exception("CancellableCall() returned an unexpected result!");
            }
        }

        private class TestCallCancellation_CancelBefore : TestBase
        {
            public override void RunTest(TestCase tCase)
            {
                var cancelSrc = new CancellationTokenSource();
                cancelSrc.Cancel();

                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var result = client.CancellableCall(TimeSpan.FromMinutes(2), cancelSrc.Token);

                if (!result)
                    throw new Exception("CancellableCall() returned an unexpected result!");
            }
        }

        private class InputStreamCancellationTest : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var cancelSrc2 = new CancellationTokenSource(500);
                var options = new StreamOptions();
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
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

                var callResult = callObj2.AsyncResult.Result.GetValueOrThrow();

                if (callResult.ItemSum != expectedSum)
                    throw new Exception("Stream call returned an unexpected result! Expected " + expectedSum + ", returned " + callResult.ItemSum);
            }
        }

        private class OutputStreamCancellationTest : TestBase
        {
            public IEnumerable<TestCase> GetCases(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                yield return CreateCase(clientDescription, client);
            }

            private TestCase CreateCase(string clientDescription, FunctionTestContract_Gen.Client client)
            {
                return new TestCase(this)
                    .SetParam("client", clientDescription, client);
            }

            public override void RunTest(TestCase tCase)
            {
                var cancelSrc = new CancellationTokenSource(500);
                var options = new StreamOptions() { WindowSize = 10 };
                var itemCount = 100;
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var callObj = client.TestOutStream(options, TimeSpan.FromMilliseconds(100), itemCount, StreamTestOptions.InvokeCompletion);

                var e = callObj.OutputStream.GetEnumerator(cancelSrc.Token);
                var receivedItemsSum = 0;
                var totalSum = (itemCount + 1) * itemCount / 2;

                while (true)
                {
                    if (!e.MoveNextAsync().Result)
                        break;

                    receivedItemsSum += e.Current;
                }

                var result = callObj.AsyncResult.Result.GetValueOrThrow();

                if (receivedItemsSum >= totalSum)
                    throw new Exception("Stream call returned an unexpected result!");

                if (result.ExitCode != StreamCallExitCode.StreamWriteCancelled)
                    throw new Exception("Stream call returned an unexpected exit code!");
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
            public override void RunTest(TestCase tCase)
            {
                var client = tCase.GetParam<FunctionTestContract_Gen.Client>("client");
                var userProp = client.GetSessionSharedProperty("UserName");
                var tokenProp = client.GetSessionSharedProperty("AuthToken");

                if (userProp != "Admin")
                    throw new Exception("UserName property does not match!");

                if (tokenProp != "15")
                    throw new Exception("AuthToken property does not match!");
            }
        }

        private abstract class ConnectionTest : TestBase
        {
            public ConnectionTest(string address, bool ssl)
            {
                Address = address;
                Ssl = ssl;
            }

            public string Address { get; }
            public bool Ssl { get; }

            protected FunctionTestContract_Gen.Client CreateClient()
            {
                return FunctionTest.CreateClient(Address, Ssl);
            }
        }

        private class SessionDropByServerTest : ConnectionTest
        {
            public SessionDropByServerTest(string address, bool ssl) : base(address, ssl) { }

            public IEnumerable<TestCase> GetCases(string clientName)
            {
                yield return new TestCase(this)
                    .SetParam("OnCloseDeleay", TimeSpan.FromSeconds(0))
                    .SetParam("client", clientName);

                yield return new TestCase(this)
                    .SetParam("OnCloseDeleay", TimeSpan.FromSeconds(13))
                    .SetParam("client", clientName);
            }

            public override void RunTest(TestCase tCase)
            {
                var delay = tCase.GetParam<TimeSpan>("OnCloseDeleay");

                var client = CreateClient();
                client.Channel.DeinitializingSession += (s, a) => Task.Delay(delay);
                client.Try.DropSession();
                client.Channel.CloseAsync().Wait();

                if (delay.TotalMilliseconds == 0)
                {
                    if (client.Channel.Fault.Code != RpcRetCode.ChannelClosedByOtherSide)
                        throw new Exception("Unexpected channel fault: " + client.Channel.Fault.Code);
                }
                else
                {
                    if (client.Channel.Fault.Code != RpcRetCode.ChannelClosedByOtherSide)
                        throw new Exception("Disconnect timeout is not working!");
                }
            }
        }

        private class ConnectActionAbortTest : ConnectionTest
        {
            public ConnectActionAbortTest(string address, bool ssl) : base(address, ssl) { }

            public IEnumerable<TestCase> GetCases(string clientName)
            {
                yield return new TestCase(this)
                    .SetParam("client", clientName);
            }

            public override void RunTest(TestCase tCase)
            {
                var client = CreateClient();

                var startTask = client.Channel.TryConnectAsync();
                var stopTask = client.Channel.CloseAsync();

                startTask.ToTask().Wait();
                stopTask.Wait();

                if (client.Channel.Fault.Code != RpcRetCode.ChannelClosed)
                    throw new Exception("Unexpected channel fault: " + client.Channel.Fault.Code);
            }
        }

        //private class InputStreamConnectTest : ConnectionTest
        //{
        //    public InputStreamConnectTest(string address, bool ssl) : base(address, ssl) { }

        //    public IEnumerable<TestCase> GetCases(string caseName, bool preconnect, bool existingAddress,
        //        Func<FunctionTestContract_Gen.Client> clientFactory)
        //    {
        //        yield return new TestCase(this)
        //            .SetHiddenParam("clientFactory", clientFactory)
        //            .SetHiddenParam("preconnect", preconnect)
        //            .SetHiddenParam("existingAddress", existingAddress)
        //            .SetParam("Name", caseName);
        //    }

        //    public override void RunTest(TestCase tCase)
        //    {
        //        try
        //        {
        //            var callObj = client.TestOutStream(new SharpRpc.StreamOptions(), TimeSpan.Zero, 1600, StreamTestOptions.None);

        //            var e = callObj.OutputStream.GetEnumerator();
        //            var count = 0;

        //            while (e.MoveNextAsync().Result)
        //                count++;

        //            if (count != 1600)
        //                throw new Exception();
        //        }
        //        catch (RpcException ex)
        //        {
        //            if (ex.ErrorCode == RpcRetCode.HostNotFound && !existingAddress)
        //                return;

        //            throw;
        //        }
        //    }
        //}

        public class CallbackHandler : FunctionTestContract_Gen.CallbackServiceBase
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
