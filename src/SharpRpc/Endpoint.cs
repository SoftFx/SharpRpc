using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public abstract class Endpoint
    {
        protected readonly object _stateLockObj = new object();
        private int _rxSegmentSize = ushort.MaxValue * 10;
        private int _txSegmentSize = ushort.MaxValue * 10;
        private TimeSpan _rxTimeout = TimeSpan.FromMinutes(1);
        private ConcurrencyMode _rxConcurrency = ConcurrencyMode.PagedQueueX1;

        public Endpoint()
        {
        }

        public int RxSegmentSize
        {
            get => _rxSegmentSize;
            set
            {
                lock (_stateLockObj)
                {
                    CheckifConfigPossible();
                    _rxSegmentSize = value;
                }
            }
        }

        public int TxSegmentSize
        {
            get => _txSegmentSize;
            set
            {
                lock (_stateLockObj)
                {
                    CheckifConfigPossible();
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
                    CheckifConfigPossible();
                    _rxTimeout = value;
                }
            }
        }

        public ConcurrencyMode RxConcurrencyMode
        {
            get => _rxConcurrency;
            set
            {
                lock (_stateLockObj)
                {
                    CheckifConfigPossible();
                    _rxConcurrency = value;
                }
            }
        }

        internal bool IsKeepAliveEnabled => KeepAliveThreshold.Ticks > 0;
        internal TimeSpan KeepAliveThreshold { get; private set; }

        public void EnableKeepAlive(TimeSpan threashold)
        {
            lock (_stateLockObj)
            {
                CheckifConfigPossible();
                KeepAliveThreshold = threashold;
            }
        }

        protected void CheckifConfigPossible()
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
