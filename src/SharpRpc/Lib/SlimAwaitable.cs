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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    /// <summary>
    /// Beware: This class is not thread-safe!
    /// </summary>
    internal class SlimAwaitable
    {
        private readonly object _lockObj;
        private bool _isCompleted;
        private Action _callback;
#if DEBUG
        private int _callbackCount;
#endif

        public SlimAwaitable(object lockObj)
        {
            _lockObj = lockObj;
        }

        public Token GetAwaiter()
        {
            return new Token(this);
        }

        public void Reset()
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            _isCompleted = false;
            _callback = null;
#if DEBUG
            _callbackCount = 0;
#endif
        }

        public void SetCompleted()
        {
            Debug.Assert(Monitor.IsEntered(_lockObj));

            _isCompleted = true;
#if DEBUG
            if (_callback != null)
            {
                Debug.Assert(_callbackCount == 0);
                _callback.Invoke();
                _callbackCount++;
            }
#else
            _callback?.Invoke();
#endif
        }

        public struct Token : INotifyCompletion
        {
            private SlimAwaitable _parent;

            public bool IsCompleted => _parent._isCompleted;

            public Token(SlimAwaitable awaitable)
            {
                _parent = awaitable;
            }

            public void OnCompleted(Action continuation)
            {
                bool immediateCall;

                lock (_parent._lockObj)
                {
                    if (!_parent._isCompleted)
                    {
                        immediateCall = false;
                        Debug.Assert(_parent._callback == null);
                        _parent._callback = continuation;
                    }
                    else
                        immediateCall = true;
                }

                if (immediateCall)
                    continuation();
            }

            public void GetResult()
            {
            }
        }
    }

    internal class SlimAwaitable<T>
    {
        private readonly object _lockObj = new object();
        private T _result;
        private bool _isCompleted;
        private Action _callback;
#if DEBUG
        private int _callbackCount;
#endif

        public SlimAwaitable(object lockObject)
        {
            _lockObj = lockObject;
        }

        public Token GetAwaiter()
        {
            return new Token(this);
        }

        public void Reset()
        {
            //Debug.Assert(Monitor.IsEntered(_lockObj));

            lock (_lockObj)
            {
#if DEBUG
                Debug.Assert(_callback == null || _callbackCount > 0); // call back must be called
                _callbackCount = 0;
#endif
                _result = default(T);
                _isCompleted = false;
                _callback = null;
            }
        }

        public void SetCompleted(T result, bool notifyViaThreadPool = false)
        {
            //Debug.Assert(Monitor.IsEntered(_lockObj));

            Action callbackCopy;

            lock (_lockObj)
            {
                _result = result;
                _isCompleted = true;
                callbackCopy = _callback;
#if DEBUG
                if (_callback != null)
                {
                    Debug.Assert(_callbackCount == 0);
                    _callbackCount++;
                }
#endif
            }

            if (callbackCopy != null)
            {
                if (notifyViaThreadPool)
                    Task.Factory.StartNew(callbackCopy);
                else
                    callbackCopy();
            }
        }

        public struct Token : INotifyCompletion
        {
            private SlimAwaitable<T> _parent;

            public bool IsCompleted => _parent._isCompleted;

            public Token(SlimAwaitable<T> awaitable)
            {
                _parent = awaitable;
            }

            public void OnCompleted(Action continuation)
            {
                bool immediateCall;

                lock (_parent._lockObj)
                {
                    if (!_parent._isCompleted)
                    {
                        immediateCall = false;
                        Debug.Assert(_parent._callback == null);
                        _parent._callback = continuation;
                    }
                    else
                        immediateCall = true;
                }

                if (immediateCall)
                    continuation();
            }

            public T GetResult()
            {
                return _parent._result;
            }
        }
    }
}
