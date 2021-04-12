using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public abstract class Endpoint
    {
        protected readonly object _stateLockObj = new object();
        private int _rxSegmentSize = ushort.MaxValue * 5;
        private int _txSegmentSize = ushort.MaxValue * 5;
        private TimeSpan _rxTimeout = TimeSpan.FromMinutes(1);
        private ConcurrencyMode _rxConcurrency = ConcurrencyMode.PagedQueueX1;

        public Endpoint()
        {
        }

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

        public ConcurrencyMode RxConcurrencyMode
        {
            get => _rxConcurrency;
            set
            {
                lock (_stateLockObj)
                {
                    ThrowIfImmutable();
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
