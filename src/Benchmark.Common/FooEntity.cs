// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using MessagePack;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Benchmark.Common
{
    [DataContract]
    [ProtoContract]
    [MessagePackObject]
    public class FooEntity : global::System.IDisposable
    {
        [ProtoMember(1)]
        [Key(1)]
        [DataMember]
        public string Symbol { get; set; }

        [ProtoMember(2)]
        [Key(2)]
        public double Bid { get; set; }

        [ProtoMember(3)]
        [Key(3)]
        public double Ask { get; set; }

        [ProtoMember(4)]
        [Key(4)]
        public DateTime Created { get; set; }

        [ProtoMember(5)]
        [Key(5)]
        public List<FooSubEntity> BidBook { get; set; } = new List<FooSubEntity>();

        [ProtoMember(6)]
        [Key(6)]
        public List<FooSubEntity> AskBook { get; set; } = new List<FooSubEntity>();

        public void Dispose()
        {

        }
    }

    [ProtoContract]
    [MessagePackObject]
    public class FooSubEntity
    {
        [ProtoMember(1)]
        [Key(1)]
        public double Price { get; set; }

        [ProtoMember(2)]
        [Key(2)]
        public double Volume { get; set; }
    }
}
