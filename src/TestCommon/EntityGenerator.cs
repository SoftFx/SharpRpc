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

namespace TestCommon
{
    public static class EntityGenerator
    {
        public static IEnumerable<FooEntity> GenerateRandomEntities()
        {
            var rnd = new Random();

            for (int i = 0; i < 10000; i++)
                yield return GenerateEntity(rnd);
        }

        public static EntitySet<FooEntity> GenerateRandomSet()
        {
            return new EntitySet<FooEntity>(GenerateRandomEntities());
        }

        public static EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdate> GenerateRandomPrebuiltSet()
        {
            var prebuilder = new BenchmarkContract_Gen.Prebuilder();
            return new EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdate>(
                GenerateRandomEntities().Select(e => prebuilder.PrebuildSendUpdate(e)));
        }

        public static void GenerateSets(out EntitySet<FooEntity> set, out EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient> prebuiltSet)
        {
            var prebuilder = new BenchmarkContract_Gen.Prebuilder();
            var entities = GenerateRandomEntities();
            
            set = new EntitySet<FooEntity>(entities);

            prebuiltSet = new EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdateToClient>(
                entities.Select(e => prebuilder.PrebuildSendUpdateToClient(e)));
        }

        private static FooEntity GenerateEntity(Random rnd)
        {
            var entity = new FooEntity();
            entity.Created = DateTime.Now;
            entity.Bid = rnd.NextDouble();
            entity.Ask = rnd.NextDouble();
            entity.Symbol = "EURUSD";
            entity.BidBook = GenerateBook(rnd);
            entity.AskBook = GenerateBook(rnd);
            return entity;
        }

        private static List<FooSubEntity> GenerateBook(Random rnd)
        {
            int size = 5;

            var list = new List<FooSubEntity>(size);

            for (int i = 0; i < size; i++)
            {
                var subEntity = new FooSubEntity();
                subEntity.Price = rnd.NextDouble();
                subEntity.Volume = rnd.NextDouble();
                list.Add(subEntity);
            }

            return list;
        }
    }
}
