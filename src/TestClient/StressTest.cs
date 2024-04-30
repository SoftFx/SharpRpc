// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TestCommon;
using TestCommon.Lib;
using TestCommon.StressTest;

namespace TestClient
{
    public class StressTest
    {
        private readonly string _address;
        private readonly List<Task> _workers = new List<Task>();
        private readonly CancellationTokenSource _stopSrc = new CancellationTokenSource();
        private readonly List<WorkerState> _states = new List<WorkerState>();

        private readonly object _statLockObj = new object();

        private int _connectionCount;
        private int _callCount;
        private int _entityCount;

        private Task _monitorTask;

        public StressTest(string serverAddress)
        {
            _address = serverAddress;
        }

        public int ParallelConnections { get; set; }
        public int MaxParallelRequests { get; set; }
        //public int RequestsToExec { get; set; }
        public int MaxRequestsPerSession { get; set; }
        public int MaxItemsPerCall { get; set; }
        public double FaultRatio { get; set; }

        public List<string> Errors { get; } = new List<string>();

        public void Start()
        {
            var rnd = new Random();

            for (int i = 0; i < ParallelConnections; i++)
            {
                var seed = rnd.Next();
                _states.Add(WorkerState.Idle);
                _workers.Add(Worker(i, seed));
            }

            _monitorTask = MonitorLoop();
        }

        public void Stop()
        {
            _stopSrc.Cancel();
            Task.WaitAll(_workers.ToArray());
            _workers.Clear();

            _monitorTask.Wait();
        }

        private void RegisterError(string error)
        {
            lock (Errors) Errors.Add(error);
        }

        public void PrintTopErrors(int max)
        {
            int toPrint = Math.Min(max, Errors.Count);

            for (int i = 0; i < toPrint; i++)
                Console.WriteLine(Errors[i]);
        }

        private async Task MonitorLoop()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(1000, _stopSrc.Token);

                    int connections, items, calls;

                    lock (_statLockObj)
                    {
                        connections = _connectionCount;
                        items = _entityCount;
                        calls = _callCount;
                        _connectionCount = 0;
                        _entityCount = 0;
                        _callCount = 0;
                    }

                    Console.WriteLine("Connections: {0}, calls: {1}, items: {2}", connections, calls, items);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task Worker(int no, int rndSeed)
        {
            var rnd = new Random(rndSeed);

            await Task.Yield();

            while (!_stopSrc.IsCancellationRequested)
            {
                try
                {
                    var reqNumber = rnd.Next(1, MaxRequestsPerSession);
                    var reqParallelism = rnd.Next(1, MaxParallelRequests);

                    var endpoint = new TcpClientEndpoint(_address, 813, TcpSecurity.None);
                    var callbackHandler = new CallbackHandler();
                    var client = StressTestContract_Gen.CreateClient(endpoint, callbackHandler);

                    _states[no] = WorkerState.Connecting;

                    lock (_statLockObj)
                        _connectionCount++;

                    var cResult = await client.Channel.TryConnectAsync();
                    if (!cResult.IsOk)
                    {
                        RegisterError("Failed to connect: " + cResult.FaultMessage);
                        await Task.Delay(1000);
                        continue;
                    }

                    _states[no] = WorkerState.GeneratingTasks;

                    var execBlockOptions = new ExecutionDataflowBlockOptions()
                    { MaxDegreeOfParallelism = reqParallelism, CancellationToken = _stopSrc.Token };
                    var execBlock = new ActionBlock<StressTask>(r => ExecRequest(r, client, callbackHandler), execBlockOptions);

                    for (int i = 0; i < reqNumber; i++)
                        execBlock.Post(GenerateRnadomTask(rnd));

                    _states[no] = WorkerState.WaitingCompletion;

                    execBlock.Complete();

                    await execBlock.Completion;

                    _states[no] = WorkerState.Disconnecting;

                    await client.Channel.CloseAsync();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    RegisterError("Worker failed: " + ex.Message);
                }

                _states[no] = WorkerState.Closed;
            }
        }

        private StressTask GenerateRnadomTask(Random rnd)
        {
            var shouldCancel = rnd.Next(0, 3) == 0;

            return new StressTask()
            {
                RequestId = Guid.NewGuid(),
                Type = RequestType.RequestResponse, // rnd.Pick(RequestType.RequestResponse, RequestType.CallbackMessages, RequestType.Downstream, RequestType.Upstream),
                MessageCount = 1000, // rnd.Next(1, MaxItemsPerCall),
                Fault = null,
                PerItemPauseMs = 0, // rnd.Next(0, 300),
                CancelAfterMs = -1, // shouldCancel ? rnd.Next(0, MaxItemsPerCall * 300) : -1
            };
        }

        private async Task ExecRequest(StressTask task, StressTestContract_Gen.Client client, CallbackHandler callback)
        {
            if (task.Type == RequestType.CallbackMessages)
            {
                await CallbackMessages(task, client, callback);
            }
            else if (task.Type == RequestType.Downstream)
            {
                await Downstream(task, client, callback);
            }
            else if (task.Type == RequestType.Upstream)
            {
                await Upstream(task, client);
            }
            else if (task.Type == RequestType.RequestResponse)
            {
                await RequestResponse(task, client);   
            }
            else if (task.Type == RequestType.DuplexStream)
            {
            }
        }

        private async Task Downstream(StressTask task, StressTestContract_Gen.Client client, CallbackHandler callback)
        {
            var streamOpt = new StreamOptions() { WindowSize = 10 };
            var call = client.DownstreamEntities(streamOpt, task.GetRequestConfig(), task.MessageCount);

            var e = call.OutputStream.GetEnumerator();
            var rxItemsCount = 0;

            while (await e.MoveNextAsync())
                rxItemsCount++;

            if (rxItemsCount != task.MessageCount)
                RegisterError("Received " + rxItemsCount + " items while requested " + task.MessageCount + " items");

            lock (_statLockObj)
            {
                _callCount++;
                _entityCount += rxItemsCount;
            }
        }

        private async Task Upstream(StressTask task, StressTestContract_Gen.Client client)
        {
            var streamOpt = new StreamOptions() { WindowSize = 10 };
            var call = client.UpstreamEntities(streamOpt, task.GetRequestConfig());
            var generator = new StressEntityGenerator();

            var sentItems = 0;
            var notSentItems = 0;

            for (int i = 0; i < task.MessageCount; i++)
            {
                var item = generator.Next();

                var writeResult = await call.InputStream.WriteAsync(item);
                if (writeResult.IsOk)
                    sentItems++;
                else
                    notSentItems++;
            }

            await call.InputStream.CompleteAsync();

            var result = await call.AsyncResult;

            if (result.FaultMessage == null && (result.Code != RpcRetCode.Ok && result.Code != RpcRetCode.OperationCanceled))
                RegisterError("Upstream failed: " + result.FaultMessage);

            if (result.FaultMessage != null && result.Code != RpcRetCode.RequestFault)
                RegisterError("Upstream failed: " + result.FaultMessage);

            if (result.Value + notSentItems != task.MessageCount)
                RegisterError($"Service received {result.Value} items while client sent {task.MessageCount - notSentItems} items");

            lock (_statLockObj)
            {
                _callCount++;
                _entityCount += result.Value;
            }
        }

        private async Task BinUpstream(StressTask task, StressTestContract_Gen.Client client)
        {
            var streamOpt = new StreamOptions() { WindowSize = 10 };
            var call = client.UpstreamEntities(streamOpt, task.GetRequestConfig());
            var generator = new StressEntityGenerator();

            var sentItems = 0;
            var notSentItems = 0;

            for (int i = 0; i < task.MessageCount; i++)
            {
                var item = generator.Next();

                var writeResult = await call.InputStream.WriteAsync(item);
                if (writeResult.IsOk)
                    sentItems++;
                else
                    notSentItems++;
            }

            await call.InputStream.CompleteAsync();

            var result = await call.AsyncResult;

            if (result.FaultMessage == null && (result.Code != RpcRetCode.Ok && result.Code != RpcRetCode.OperationCanceled))
                RegisterError("Upstream failed: " + result.FaultMessage);

            if (result.FaultMessage != null && result.Code != RpcRetCode.RequestFault)
                RegisterError("Upstream failed: " + result.FaultMessage);

            if (result.Value + notSentItems != task.MessageCount)
                RegisterError($"Service received {result.Value} items while client sent {task.MessageCount - notSentItems} items");

            lock (_statLockObj)
            {
                _callCount++;
                _entityCount += result.Value;
            }
        }

        private async Task RequestResponse(StressTask task, StressTestContract_Gen.Client client)
        {
            var generator = new StressEntityGenerator();
            var callResult = await client.TryAsync.RequestResponse(generator.Next(), task.GetRequestConfig());

            if (callResult.Code != RpcRetCode.Ok)
                RegisterError("RequestResponse call failed: " + callResult.FaultMessage);

            lock (_statLockObj)
            {
                _callCount++;
                _entityCount += 2;
            }
        }

        private async Task CallbackMessages(StressTask task, StressTestContract_Gen.Client client, CallbackHandler callback)
        {
            var callResult = await client.TryAsync.RequestMessages(task.MessageCount, task.GetRequestConfig());

            if (callResult.Code != RpcRetCode.Ok)
                RegisterError("CallbackMessages call failed: " + callResult.FaultMessage);

            var rxMessages = callback.GetCount(task.RequestId);

            if (rxMessages != task.MessageCount)
                RegisterError("Received " + rxMessages + "messages  while requested " + task.MessageCount + " messages");

            lock (_statLockObj)
            {
                _callCount++;
                _entityCount += rxMessages;
            }
        }

        public enum RequestType
        {
            RequestResponse,
            Downstream,
            Upstream,
            DuplexStream,
            BinDownstream,
            BinUpstream,
            CallbackMessages
        }

        public enum WorkerState
        {
            Idle,
            Connecting,
            GeneratingTasks,
            WaitingCompletion,
            Disconnecting,
            Closed
        }

        public struct StressTask
        {
            public Guid RequestId { get; set; }
            public RequestType Type { get; set; }
            public string Fault { get; set; }
            public int MessageCount { get; set; }
            public int CancelAfterMs { get; set; }
            public int PerItemPauseMs { get; set; }

            public RequestConfig GetRequestConfig()
            {
                return new RequestConfig() { Id = RequestId, Fault = Fault, CancelAfterMs = CancelAfterMs, PerItemPauseMs = PerItemPauseMs };
            }
        }

        private class CallbackHandler : StressTestContract_Gen.CallbackServiceBase
        {
            private readonly ConcurrentDictionary<Guid, int> _messageCount = new ConcurrentDictionary<Guid, int>();

#if NET5_0_OR_GREATER
            public override ValueTask CallbackMessage(Guid requestId, StressEntity entity)
#else
            public override Task CallbackMessage(Guid requestId, StressEntity entity)
#endif
            {
                _messageCount.AddOrUpdate(requestId, 1, (id, c) => c + 1);
                return FwAdapter.AsyncVoid;
            }

            public int GetCount(Guid requestId)
            {
                _messageCount.TryGetValue(requestId, out var count);
                return count;
            }
        }
    }
}
