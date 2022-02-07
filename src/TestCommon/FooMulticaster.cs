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
        private readonly List<IListener> _listeners = new List<IListener>();

        public FooMulticaster()
        {
            EntityGenerator.GenerateSets(out _entitySet, out _prebuildEntitySet);
        }

        public void Add(BenchmarkContract_Gen.CallbackClient listener)
        {
            Add(new CallbackAdapter(listener));
        }

        public void Add(StreamWriter<FooEntity> listener)
        {
            Add(new StreamAdapter(listener));
        }

        public void Remove(BenchmarkContract_Gen.CallbackClient listener)
        {
            FindAndRemove(listener);
        }

        public void Remove(StreamWriter<FooEntity> listener)
        {
            FindAndRemove(listener);
        }

        public Task<MulticastReport> Multicast(int msgCount, bool usePrebuiltMessages)
        {
            lock (_listeners)
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
                        for (int l = 0; l < _listeners.Count; l++)
                        {
                            var sendResult = _listeners[l].Send(_prebuildEntitySet.Next());
                            if (!sendResult.IsOk)
                                failed++;
                            else
                                sent++;
                        }
                    }
                    else
                    {
                        var entity = _entitySet.Next();
                        foreach (var l in _listeners)
                        //Parallel.ForEach(_listeners, l =>
                        {
                            var sendResult = l.Send(entity);
                            if (!sendResult.IsOk)
                                failed++;
                            else
                                sent++;
                        }//);
                    }
                }

                lock (_listeners)
                    _isBusy = false;

                watch.Stop();

                return new MulticastReport { MessageFailed = failed, MessageSent = sent, Elapsed = watch.Elapsed };
            }, TaskCreationOptions.LongRunning);
        }

        private void CheckIfIsBusy()
        {
            if (_isBusy)
                throw new Exception("Multicaster is busy and cannot do anything more!");
        }

        private void Add(IListener listener)
        {
            lock (_listeners)
            {
                CheckIfIsBusy();
                _listeners.Add(listener);
            }
        }

        private void FindAndRemove(object listenerObj)
        {
            lock (_listeners)
            {
                CheckIfIsBusy();
                _listeners.RemoveAll(l => l.OriginalListener == listenerObj);
            }
        }

        private interface IListener
        {
            object OriginalListener { get; }
            RpcResult Send(FooEntity update);
            RpcResult Send(BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient update);
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

        private class StreamAdapter : IListener
        {
            private readonly StreamWriter<FooEntity> _stub;

            public StreamAdapter(StreamWriter<FooEntity> callbackStub)
            {
                _stub = callbackStub;
            }

            public object OriginalListener => _stub;

            public RpcResult Send(FooEntity update)
            {
                return _stub.WriteAsync(update).Result;
            }

            public RpcResult Send(BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient update)
            {
                throw new NotImplementedException();
            }
        }
    }
}
