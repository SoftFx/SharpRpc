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
using System.Threading.Tasks;

namespace TestCommon
{
    public class FooMulticaster
    {
        private bool _isBusy;
        private readonly EntitySet<FooEntity> _entitySet;
        private readonly EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient> _prebuildEntitySet;
        private readonly List<IListener> _msgListeners = new List<IListener>();
        private readonly List<IStreamListener> _streamListeners = new List<IStreamListener>();

        public FooMulticaster()
        {
            EntityGenerator.GenerateSets(out _entitySet, out _prebuildEntitySet);
        }

        public void Add(BenchmarkContract_Gen.CallbackClient listener)
        {
            Add(new CallbackAdapter(listener));
        }

        public StreamAdapter Add(StreamWriter<FooEntity> listener)
        {
            var adapter = new StreamAdapter(listener);
            Add(adapter);
            return adapter;
        }

        public void Remove(BenchmarkContract_Gen.CallbackClient listener)
        {
            FindAndRemove(listener);
        }

        public void Remove(StreamWriter<FooEntity> listener)
        {
            FindAndRemove(listener);
        }

        public Task<MulticastReport> MulticastMessages(int msgCount, bool usePrebuiltMessages)
        {
            lock (_msgListeners)
            {
                CheckIfIsBusy();
                _isBusy = true;
            }

            return Task.Factory.StartNew(() =>
            {
                var failed = 0;
                var sent = 0;

                var watch = Stopwatch.StartNew();

                for (int i = 0; i < msgCount; i++)
                {
                    if (usePrebuiltMessages)
                    {
                        for (int l = 0; l < _msgListeners.Count; l++)
                        {
                            var sendResult = _msgListeners[l].Send(_prebuildEntitySet.Next());
                            if (!sendResult.IsOk)
                                failed++;
                            else
                                sent++;
                        }
                    }
                    else
                    {
                        var entity = _entitySet.Next();
                        for (int l = 0; l < _msgListeners.Count; l++)
                        {
                            var sendResult = _msgListeners[l].Send(entity);
                            if (!sendResult.IsOk)
                                failed++;
                            else
                                sent++;
                        }
                    }
                }

                lock (_msgListeners)
                    _isBusy = false;

                watch.Stop();

                return new MulticastReport { MessageFailed = failed, MessageSent = sent, Elapsed = watch.Elapsed };
            }, TaskCreationOptions.LongRunning);
        }

        public async Task<MulticastReport> MulticastStreamItems(int msgCount)
        {
            try
            {
                lock (_msgListeners)
                {
                    CheckIfIsBusy();
                    _isBusy = true;
                }

                var failed = 0;
                var sent = 0;

                Console.WriteLine($"Multicast start (msg x{msgCount}, listeners x{_msgListeners.Count})");

                var watch = Stopwatch.StartNew();

                for (int i = 0; i < msgCount; i++)
                {
                    var entity = _entitySet.Next();

                    foreach (var listener in _streamListeners)
                    {
                        var sendResult = await listener.Send(entity);
                        if (!sendResult.IsOk)
                            failed++;
                        else
                            sent++;
                    }
                }

                Console.WriteLine("Multicast stream close");

                await Task.WhenAll(_streamListeners.Select(l => l.Close()));

                //foreach (var listener in _streamListeners)
                //    await listener.Close();

                _streamListeners.Clear();

                lock (_msgListeners)
                    _isBusy = false;

                watch.Stop();

                Console.WriteLine("Multicast end");

                return new MulticastReport { MessageFailed = failed, MessageSent = sent, Elapsed = watch.Elapsed };
            }
            catch (Exception ex)
            {
                Console.WriteLine("MulticastStreamItems() failed! " + ex);
                throw;
            }
        }

        private void CheckIfIsBusy()
        {
            if (_isBusy)
                throw new Exception("Multicaster is busy and cannot do anything more!");
        }

        private void Add(IListener listener)
        {
            lock (_msgListeners)
            {
                CheckIfIsBusy();
                _msgListeners.Add(listener);
            }
        }

        private void Add(IStreamListener listener)
        {
            lock (_msgListeners)
            {
                CheckIfIsBusy();
                _streamListeners.Add(listener);
            }
        }

        private void FindAndRemove(object listenerObj)
        {
            lock (_msgListeners)
            {
                CheckIfIsBusy();
                _msgListeners.RemoveAll(l => l.OriginalListener == listenerObj);
            }
        }

        private interface IListener
        {
            object OriginalListener { get; }
            RpcResult Send(FooEntity update);
            RpcResult Send(BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient update);
        }

        private interface IStreamListener
        {
#if NET5_0_OR_GREATER
            ValueTask<RpcResult> Send(FooEntity update);
#else
            Task<RpcResult> Send(FooEntity update);
#endif
            Task Close();
        }

        private class CallbackAdapter : IListener
        {
            private readonly BenchmarkContract_Gen.CallbackClient _stub;

            public CallbackAdapter(BenchmarkContract_Gen.CallbackClient callbackStub)
            {
                _stub = callbackStub;
            }

            public object OriginalListener => _stub;

            public RpcResult Send(FooEntity update)
            {
                return _stub.Try.SendUpdateToClient(update);
            }

            public RpcResult Send(BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient update)
            {
                return _stub.Try.SendUpdateToClient(update);
            }
        }

        public class StreamAdapter : IStreamListener
        {
            private readonly StreamWriter<FooEntity> _stub;
            private readonly TaskCompletionSource<bool> _completionSrc = new TaskCompletionSource<bool>();

            public StreamAdapter(StreamWriter<FooEntity> callbackStub)
            {
                _stub = callbackStub;
            }

            public Task Completion => _completionSrc.Task;

            public async Task Close()
            {
                await _stub.CompleteAsync();
                _completionSrc.SetResult(true);
            }

#if NET5_0_OR_GREATER
            public ValueTask<RpcResult> Send(FooEntity update)
#else
            public Task<RpcResult> Send(FooEntity update)
#endif
            {
                return _stub.WriteAsync(update);
            }
        }
    }
}
