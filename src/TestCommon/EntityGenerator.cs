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
    public class EntityGenerator
    {
        private readonly Random _rnd = new Random();
        private readonly List<FooEntity> _entitiesCache = new List<FooEntity>();
        private int _index = -1;

        public EntityGenerator()
        {
            for (int i = 0; i < 10000; i++)
                _entitiesCache.Add(Generate());
        }

        public FooEntity Next()
        {
            _index++;
            if (_index >= _entitiesCache.Count)
                _index = 0;
            return _entitiesCache[_index];
        }

        private FooEntity Generate()
        {
            var entity = new FooEntity();
            entity.Created = DateTime.Now;
            entity.Bid = _rnd.NextDouble();
            entity.Ask = _rnd.NextDouble();
            entity.Symbol = "EURUSD";
            entity.BidBook = GenerateBook();
            entity.AskBook = GenerateBook();
            return entity;
        }

        private List<FooSubEntity> GenerateBook()
        {
            int size = 5;

            var list = new List<FooSubEntity>(size);

            for (int i = 0; i < size; i++)
            {
                var subEntity = new FooSubEntity();
                subEntity.Price = _rnd.NextDouble();
                subEntity.Volume = _rnd.NextDouble();
                list.Add(subEntity);
            }

            return list;
        }
    }
}
