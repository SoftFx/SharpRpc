using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public abstract class Endpoint
    {
        protected readonly object _stateLockObj = new object();
        private int _rxSegmentSize = ushort.MaxValue;
        private int _txSegmentSize = ushort.MaxValue;
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
