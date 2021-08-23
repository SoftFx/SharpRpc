// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class Endpoint : IConfigHost
    {
        protected readonly object _stateLockObj = new object();
        private int _rxSegmentSize = ushort.MaxValue * 1;
        private int _txSegmentSize = ushort.MaxValue * 1;
        private TimeSpan _rxTimeout = TimeSpan.FromMinutes(1);
        private bool _asyncMessageParse = false;
        private object _owner;
        private TaskScheduler _scheduler = null;

        public Endpoint()
        {
            Name = Namer.GetInstanceName(GetType());
            Dispatcher = new MessageDispatcherConfig(this);
        }

        public MessageDispatcherConfig Dispatcher { get; }

        public string Name { get; }

        /// <summary>
        /// 
        /// </summary>
        public int RxBufferSegmentSize
        {
            get => _rxSegmentSize;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _rxSegmentSize = value;
                }
            }
        }

        /// <summary>
        /// Enabling this option moves message parsing to a separate thread.
        /// Otherwise, message are parsed in the same thread they are recieved. (Default)
        /// Improves channel bandwidth in a cost of higher CPU consumption.
        /// </summary>
        public bool AsyncMessageParsing
        {
            get => _asyncMessageParse;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _asyncMessageParse = value;
                }
            }
        }

        public int TxBufferSegmentSize
        {
            get => _txSegmentSize;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _txSegmentSize = value;
                }
            }
        }

        public TimeSpan RxTimeout
        {
            get => _rxTimeout;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _rxTimeout = value;
                }
            }
        }

        public TaskScheduler TaskScheduler
        {
            get => _scheduler;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
                    _scheduler = value;
                }
            }
        }

        internal bool IsKeepAliveEnabled => KeepAliveThreshold.Ticks > 0;
        internal TimeSpan KeepAliveThreshold { get; private set; }
        internal TaskFactory TaskQueue { get; private set; }

        internal abstract LoggerFacade LoggerAdapter { get; }

        public void EnableKeepAlive(TimeSpan threashold)
        {
            lock (_stateLockObj)
            {
                ThrowIfImmutable();
                KeepAliveThreshold = threashold;
            }
        }

        protected void ThrowIfImmutable()
        {
            Debug.Assert(Monitor.IsEntered(_stateLockObj));

            if (_owner != null)
                throw new InvalidOperationException("Endpoint configuration cannot be changed at this time! Please configure endpoint before it attached to a communication object.");
        }

        internal void Validate()
        {
            ValidateConfiguration();
        }

        protected virtual void ValidateConfiguration()
        {
        }

        object IConfigHost.SyncObject => _stateLockObj;
        void IConfigHost.ThrowIfImmutable() => ThrowIfImmutable();

        internal void LockTo(object owner)
        {
            lock (_stateLockObj)
            {
                if (_owner != null)
                    throw new InvalidOperationException("This endpoint is already in use by other communication object!");

                _owner = owner;
            }

            InitTaskScheduler();
        }

        private void InitTaskScheduler()
        {
            if (_scheduler != null)
            {
                TaskQueue = new TaskFactory(CancellationToken.None, TaskCreationOptions.HideScheduler,
                    TaskContinuationOptions.HideScheduler, _scheduler);
            }
            else
                TaskQueue = Task.Factory;
        }
    }
}
