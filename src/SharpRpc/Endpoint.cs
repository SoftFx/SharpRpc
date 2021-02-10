using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public abstract class Endpoint
    {
        protected readonly object _stateLockObj = new object();
        private IRpcSerializer _serializer;
        private int _rxSegmentSize = ushort.MaxValue;
        private int _txSegmentSize = ushort.MaxValue;

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

        public IRpcSerializer Serializer
        {
            get => _serializer;
            set
            {
                lock (_stateLockObj)
                {
                    CheckifConfigPossible();

                    _serializer = value;
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
            if (_serializer == null)
                throw new Exception("Serializer is not configured!");
        }
    }
}
