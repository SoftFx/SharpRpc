﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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
    public abstract class Endpoint : ConfigElement
    {
        private int _rxSegmentSize = ushort.MaxValue * 1;
        private int _txSegmentSize = ushort.MaxValue * 1;
        private TimeSpan _rxTimeout = TimeSpan.FromMinutes(1);
        private TimeSpan _loginTimeout = TimeSpan.FromMinutes(1);
        private TimeSpan _logoutTimeout = TimeSpan.FromMinutes(1);
        private TimeSpan _transportCloseTimeout = TimeSpan.FromMinutes(1);
        private TimeSpan _heartbeatPeriod = TimeSpan.FromSeconds(10);
        private long _maxMessageSize = long.MaxValue;
        //private bool _asyncMessageParse = false;
        private TaskScheduler _scheduler = null;

        public Endpoint()
        {
            Name = Namer.GetInstanceName(GetType());
            Dispatcher = new MessageDispatcherConfig();
            Dispatcher.AttachTo(this);
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
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    CheckGreaterZanZero(value);
                    _rxSegmentSize = value;
                }
            }
        }

        ///// <summary>
        ///// Enabling this option moves message parsing to a separate thread.
        ///// Otherwise, message are parsed in the same thread they are recieved. (Default)
        ///// Improves channel bandwidth in a cost of higher CPU consumption.
        ///// </summary>
        //public bool AsyncMessageParsing
        //{
        //    get => _asyncMessageParse;
        //    set
        //    {
        //        lock (LockObject)
        //        {
        //            ThrowIfImmutable();
        //            _asyncMessageParse = value;
        //        }
        //    }
        //}

        public int TxBufferSegmentSize
        {
            get => _txSegmentSize;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    CheckGreaterZanZero(value);
                    _txSegmentSize = value;
                }
            }
        }

        public long MaxMessageSize
        {
            get => _maxMessageSize;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    CheckGreaterZanZero(value);
                    _maxMessageSize = value;
                }
            }
        }

        /// <summary>
        /// Specifys maximum wait time for receive operation. If no messages are received within the specified timeout,
        /// the connection is considered to be lost. Use this property together with HeartbeatPeriod to establish proper
        /// connection loss detection. Specify TimeSpan.Zero to disable receive timeout.
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => _rxTimeout;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _rxTimeout = value;
                }
            }
        }

        public TimeSpan LoginTimeout
        {
            get => _loginTimeout;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _loginTimeout = value;
                }
            }
        }

        public TimeSpan LogoutTimeout
        {
            get => _logoutTimeout;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _logoutTimeout = value;
                }
            }
        }

        public TimeSpan TransportShutdownTimeout
        {
            get => _transportCloseTimeout;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _transportCloseTimeout = value;
                }
            }
        }

        public TaskScheduler TaskScheduler
        {
            get => _scheduler;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _scheduler = value;
                }
            }
        }

        internal TimeSpan HeartbeatPeriod
        {
            get => _heartbeatPeriod;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _heartbeatPeriod = value;
                }
            }
        }

        internal bool IsHeartbeatEnabled => HeartbeatPeriod > TimeSpan.Zero;
        internal TaskFactory TaskFactory { get; private set; }

        internal abstract IRpcLogger GetLogger();

        protected override void ValidateAndInitialize()
        {
            InitTaskScheduler();
        }

        private void InitTaskScheduler()
        {
            TaskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.HideScheduler,
                    TaskContinuationOptions.HideScheduler, _scheduler ?? TaskScheduler.Default);
        }

        private void CheckGreaterZanZero(long value)
        {
            if (value <= 0)
                throw new ArgumentException("A value must be greater than zero.");
        }
    }
}
