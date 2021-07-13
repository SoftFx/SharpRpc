// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public abstract class Endpoint
    {
        protected readonly object _stateLockObj = new object();
        private int _rxSegmentSize = (int)(ushort.MaxValue * 0.5);
        private int _txSegmentSize = ushort.MaxValue * 1;
        private TimeSpan _rxTimeout = TimeSpan.FromMinutes(1);
        //private ConcurrencyMode _rxConcurrency = ConcurrencyMode.PagedQueue;

        public Endpoint()
        {
            Name = Namer.GetInstanceName(GetType());
        }

        public string Name { get; }

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

        //public ConcurrencyMode RxConcurrencyMode
        //{
        //    get => _rxConcurrency;
        //    set
        //    {
        //        lock (_stateLockObj)
        //        {
        //            ThrowIfImmutable();
        //            _rxConcurrency = value;
        //        }
        //    }
        //}

        internal bool IsKeepAliveEnabled => KeepAliveThreshold.Ticks > 0;
        internal TimeSpan KeepAliveThreshold { get; private set; }

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
        }

        internal void Validate()
        {
            ValidateConfiguration();
        }

        protected virtual void ValidateConfiguration()
        {
        }
    }
}
