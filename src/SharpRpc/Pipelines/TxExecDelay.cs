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
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class TxExecDelay
    {
        private readonly object _lockObj;
        private readonly Action _workerAction;
        private bool _triggered;
        private readonly Timer _delayTimer;
        private readonly TimeSpan _delay;
        private readonly TaskFactory _taskFactory;

        public TxExecDelay(Action worker, TaskFactory taskQueue, TimeSpan delay, object lockObj)
        {
            _lockObj = lockObj;
            _workerAction = worker;
            _delay = delay;
            _taskFactory = taskQueue;
            _delayTimer = new Timer(OnDelayTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void TriggerOn()
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));
            Debug.Assert(!_triggered);

            _triggered = true;
            _delayTimer.Change(_delay, Timeout.InfiniteTimeSpan);
        }

        public void Force()
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            if (_triggered)
            {
                LaunchWorker();
                DisableTimer();
            }
        }

        private void OnDelayTimer(object state)
        {
            lock (_lockObj)
            {
                if (_triggered)
                    LaunchWorker();
            }
        }

        private void LaunchWorker()
        {
            _triggered = false;
            _taskFactory.StartNew(_workerAction);
        }

        private void DisableTimer()
        {
            _delayTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }
}
