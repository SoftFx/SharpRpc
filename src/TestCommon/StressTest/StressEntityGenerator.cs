// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCommon.StressTest
{
    public class StressEntityGenerator
    {
        private readonly Random _rnd = new Random();
        private int _idSeed;

        public int MaxArraySize { get; set; } = 100;

        public StressEntity Next()
        {
            return new StressEntity()
            {
                No = ++_idSeed,
                StrProperty = _rnd.Next().ToString(),
                StrArrayProperty = GenArray(),
                EntityProperty = new SomeOtherEntity()
            };
        }

        private List<string> GenArray()
        {
            var list = new List<string>();

            for (int i = 0; i < _rnd.Next(0, MaxArraySize); i++)
                list.Add(i.ToString());

            return list;
        }
    }
}
