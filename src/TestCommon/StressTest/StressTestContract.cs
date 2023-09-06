// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using MessagePack;
using SharpRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCommon
{
    [RpcServiceContract(EnablePrebuilder = true)]
    [RpcSerializer(SerializerChoice.MessagePack)]
    interface StressTestContract
    {
        [RpcContract(1, RpcType.Call)]
        StressEntity RequestResponse(StressEntity entity, RequestConfig cfg);

        [RpcContract(2, RpcType.Call)]
        void RequestMessages(int count, RequestConfig cfg);

        [RpcContract(3, RpcType.CallbackMessage)]
        void CallbackMessage(Guid requestId, StressEntity entity);

        [RpcContract(4, RpcType.Call)]
        [RpcStreamOutput(typeof(StressEntity))]
        void DownstreamEntities(RequestConfig cfg, int count);

        [RpcContract(5, RpcType.Call)]
        [RpcStreamInput(typeof(StressEntity))]
        int UpstreamEntities(RequestConfig cfg);

        [RpcContract(6, RpcType.Call)]
        [RpcStreamInput(typeof(StressEntity))]
        [RpcStreamOutput(typeof(StressEntity))]
        void DuplexStreamEntities(RequestConfig cfg);

        [RpcContract(7, RpcType.Message)]
        void LoadMessage(Guid requestId, StressEntity entity, bool sendBack);
    }

    [MessagePackObject]
    public class RequestConfig
    {
        [Key(1)]
        public Guid Id { get; set; }

        [Key(2)]
        public string Fault { get; set; }

        [Key(3)]
        public int PerItemPauseMs { get; set; }

        [Key(4)]
        public int CancelAfterMs { get; set; }

        [IgnoreMember]
        public bool HasItemPause => PerItemPauseMs > 0;
    }

    [MessagePackObject]
    public class StressEntity
    {
        [Key(1)]
        public int No { get; set; }

        [Key(2)]
        public string StrProperty { get; set; }

        [Key(3)]
        public List<string> StrArrayProperty { get; set; }

        [Key(4)]
        public SomeOtherEntity EntityProperty { get; set; }
    }

    [MessagePackObject]
    public class SomeOtherEntity
    {
        [Key(1)]
        public int No { get; set; }

        [Key(2)]
        public string StrProperty { get; set; }
    }
}
