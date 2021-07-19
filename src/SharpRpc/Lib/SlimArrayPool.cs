// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    internal class SlimArrayPool<T>
    {
        private readonly int _arraySize;
        private readonly int _maxCacheSize;
        private ConcurrentQueue<T[]> _cache = new ConcurrentQueue<T[]>();

        public SlimArrayPool(int arraySize, int maxCachedArrays)
        {
            _arraySize = arraySize;
            _maxCacheSize = maxCachedArrays;
        }

        public T[] Rent()
        {
            if (!_cache.TryDequeue(out var array))
                array = new T[_arraySize];

            return array;
        }

        public void Return(T[] array)
        {
            if (_cache.Count < _maxCacheSize)
                _cache.Enqueue(array);
        }
}
}
