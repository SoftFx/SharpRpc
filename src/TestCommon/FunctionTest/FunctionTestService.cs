// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpRpc;

namespace TestCommon
{
    public class FunctionTestService : FunctionTestContract_Gen.ServiceBase
    {
#if NET5_0_OR_GREATER
        public override ValueTask TestCall1(CallContext context, int p1, string p2)
#else
        public override Task TestCall1(CallContext context, int p1, string p2)
#endif
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> TestCall2(CallContext context, int p1, string p2)
#else
        public override Task<string> TestCall2(CallContext context, int p1, string p2)
#endif
        {
            if (p1 != 10 || p2 != "11")
                throw new Exception("Invalid input!");

            return FwAdapter.WrappResult("123");
        }

        public override void OnResponseSent_TestCall2(string responseValue)
        {
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> TestCrash(CallContext context, int p1, string p2)
#else
        public override Task<string> TestCrash(CallContext context, int p1, string p2)
#endif
        {
            throw new Exception("This is test unexpected expcetion.");
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> TestRpcException(CallContext context, int p1, string p2)
#else
        public override Task<string> TestRpcException(CallContext context, int p1, string p2)
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
        public override ValueTask TestCallFault(CallContext context, int faultNo)
#else
        public override Task TestCallFault(CallContext context, int faultNo)
#endif
        {
            if (faultNo == 1)
                throw RpcFaultException.Create("Fault Message 1", new TestFault1 { CustomCode = 11 });
            else
                throw RpcFaultException.Create("Fault Message 2", new TestFault2 { CustomCode = 12 });
        }

#if NET5_0_OR_GREATER
        public async override ValueTask<string> InvokeCallback(CallContext context, int callbackNo, int p1, string p2)
#else
        public async override Task<string> InvokeCallback(CallContext context, int callbackNo, int p1, string p2)
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
        public override ValueTask<List<Tuple<int>>> ComplexTypesCall(CallContext context, List<DateTime> list, List<List<DateTime>> listOfLists, Dictionary<int, int> dictionary)
#else
        public override Task<List<Tuple<int>>> ComplexTypesCall(CallContext context, List<DateTime> list, List<List<DateTime>> listOfLists, Dictionary<int, int> dictionary)
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
        public override async ValueTask<StreamCallResult> TestInStream(CallContext context, StreamReader<int> inputStream, TimeSpan delay, StreamTestOptions options)
#else
        public override async Task<StreamCallResult> TestInStream(CallContext context, StreamReader<int> inputStream, TimeSpan delay, StreamTestOptions options)
#endif
        {
            if (options == StreamTestOptions.JustExit)
                return new StreamCallResult(StreamCallExitCode.ImmediateExit, 0);
            else if (options == StreamTestOptions.ImmediateFault)
                throw new RpcFaultException("Test fault");
            else if (options == StreamTestOptions.ImmediateCustomFault)
                throw RpcFaultException.Create(new TestFault1());

            var sum = 0;

#if NET5_0_OR_GREATER
            await foreach (var i in inputStream)
            {
                sum += i;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);
            }
#else
            var e = inputStream.GetEnumerator();

            while (await e.MoveNextAsync())
            {
                sum += e.Current;

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);
            }
#endif

            return new StreamCallResult(StreamCallExitCode.StreamCompleted, sum);
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<StreamCallResult> TestOutStream(CallContext context, StreamWriter<int> outputStream, TimeSpan delay, int count, StreamTestOptions options)
#else
        public override async Task<StreamCallResult> TestOutStream(CallContext context, StreamWriter<int> outputStream, TimeSpan delay, int count, StreamTestOptions options)
#endif
        {
            if (options == StreamTestOptions.JustExit)
                return new StreamCallResult(StreamCallExitCode.ImmediateExit, 0);
            else if (options == StreamTestOptions.ImmediateFault)
                throw new RpcFaultException("Test fault");
            else if (options == StreamTestOptions.ImmediateCustomFault)
                throw RpcFaultException.Create(new TestFault1());

            for (int i = 1; i <= count; i++)
            {
                var wResult = await outputStream.WriteAsync(i);

                if (!wResult.IsOk)
                {
                    if (wResult.Code == RpcRetCode.OperationCanceled)
                        return new StreamCallResult(StreamCallExitCode.StreamWriteCancelled, 0);
                    else if (wResult.Code == RpcRetCode.StreamCompleted)
                        return new StreamCallResult(StreamCallExitCode.StreamCompleted, 0);
                    else
                        return new StreamCallResult(StreamCallExitCode.Error, 0);
                }

                if (context.CancellationToken.IsCancellationRequested)
                    return new StreamCallResult(StreamCallExitCode.StreamWriteCancelled, 0);

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);
            }

            if (options == StreamTestOptions.InvokeCompletion)
                await outputStream.CompleteAsync();

            return new StreamCallResult(StreamCallExitCode.StreamCompleted, 0);
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<int> TestDuplexStream(CallContext context, StreamReader<int> inputStream, StreamWriter<int> outputStream, TimeSpan delay, StreamTestOptions options)
#else
        public override async Task<int> TestDuplexStream(CallContext context, StreamReader<int> inputStream, StreamWriter<int> outputStream, TimeSpan delay, StreamTestOptions options)
#endif
        {
            if (options == StreamTestOptions.JustExit)
                return -2;
            else if (options == StreamTestOptions.ImmediateFault)
                throw new RpcFaultException("Test fault");
            else if (options == StreamTestOptions.ImmediateCustomFault)
                throw RpcFaultException.Create(new TestFault1());

#if NET5_0_OR_GREATER
            await foreach (var item in inputStream)
            {
                var wResult = await outputStream.WriteAsync(item);
#else
            var e = inputStream.GetEnumerator();
            while(await e.MoveNextAsync())
            {
                var wResult = await outputStream.WriteAsync(e.Current);
#endif

                if (!wResult.IsOk)
                {
                    if (wResult.Code == RpcRetCode.OperationCanceled)
                        return -1;
                    return -2;
                }

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay);
            }

            if (options == StreamTestOptions.InvokeCompletion)
                await outputStream.CompleteAsync();

            return context.CancellationToken.IsCancellationRequested ? -1 : 0;
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<bool> CancellableCall(CallContext context, TimeSpan delay)
#else
        public override async Task<bool> CancellableCall(CallContext context, TimeSpan delay)
#endif
        {
            try
            {
                await Task.Delay(delay, context.CancellationToken);
                return false;
            }
            catch (TaskCanceledException)
            {
                return true;
            }
        }

#if NET5_0_OR_GREATER
        public override ValueTask<string> GetSessionSharedProperty(CallContext context, string name)
#else
        public override Task<string> GetSessionSharedProperty(CallContext context, string name)
#endif
        {
            Session.Properties.TryGetValue(name, out var propValue);
            return FwAdapter.WrappResult(propValue?.ToString());
        }
    }
}
