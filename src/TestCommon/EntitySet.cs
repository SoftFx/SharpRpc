// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace TestCommon
{
    public class EntitySet<T>
    {
        private readonly Random _rnd = new Random();
        private readonly List<T> _entitiesCache = new List<T>();
        private int _index = -1;

        public EntitySet(IEnumerable<T> entities)
        {
            _entitiesCache.AddRange(entities);
        }

        public T Next()
        {
            _index++;
            if (_index >= _entitiesCache.Count)
                _index = 0;
            return _entitiesCache[_index];
        }
    }
}
