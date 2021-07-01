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

namespace TestCommon
{
    public class FooMulticaster
    {
        private bool _isBusy;
        private readonly EntitySet<FooEntity> _entitySet;
        private readonly EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient> _prebuildEntitySet;
        private readonly List<BenchmarkContract_Gen.CallbackClient> _listeners = new List<BenchmarkContract_Gen.CallbackClient>();

        public FooMulticaster()
        {
            EntityGenerator.GenerateSets(out _entitySet, out _prebuildEntitySet);
        }

        public void Add(BenchmarkContract_Gen.CallbackClient listener)
        {
            lock (_listeners)
            {
                CheckIfIsBusy();
                _listeners.Add(listener);
            }
        }

        public Task Multicast(int msgCount, bool usePrebuiltMessages)
        {
            lock (_listeners)
            {
                CheckIfIsBusy();
                _isBusy = true;
            }

            return Task.Factory.StartNew(() =>
            {
                var failed = 0;

                for (int i = 0; i < msgCount; i++)
                {
                    if (usePrebuiltMessages)
                    {
                        var msg = _prebuildEntitySet.Next();
                        foreach (var l in _listeners)
                        {
                            var sendResult = l.TrySendUpdateToClient(msg);
                            if (!sendResult.IsOk)
                                failed++;
                        }
                    }
                    else
                    {
                        var entity = _entitySet.Next();
                        foreach (var l in _listeners)
                        {
                            var sendResult = l.TrySendUpdateToClient(entity);
                            if (!sendResult.IsOk)
                                failed++;
                        }
                    }
                }

                lock (_listeners)
                    _isBusy = false;
            });
        }

        public void Remove(BenchmarkContract_Gen.CallbackClient listener)
        {
            lock (_listeners)
            {
                CheckIfIsBusy();
                _listeners.Add(listener);
            }
        }

        private void CheckIfIsBusy()
        {
            if (_isBusy)
                throw new Exception("Multicaster is busy and cannot do anything more!");
        }
    }
}
