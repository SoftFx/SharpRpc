﻿using MessagePack;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Common
{
    [ProtoContract]
    [MessagePackObject]
    public class FooEntity
    {
        [ProtoMember(1)]
        [Key(1)]
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