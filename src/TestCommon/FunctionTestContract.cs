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
using MessagePack;
using SharpRpc;

namespace TestCommon
{
    [RpcContract]
    [RpcSerializer(SerializerChoice.MessagePack)]
    interface FunctionTestContract
    {
        [Rpc(RpcType.Message)]
        void TestNotify1(int p1, string p2);

        [Rpc(RpcType.Call)]
        void TestCall1(int p1, string p2);

        [Rpc(RpcType.Call)]
        string TestCall2(int p1, string p2);

        [Rpc(RpcType.Call)]
        string TestCrash(int p1, string p2);

        [Rpc(RpcType.Call)]
        string TestRpcException(int p1, string p2);

        [Rpc(RpcType.Call)]
        [RpcFault(typeof(TestFault1))]
        [RpcFault(typeof(TestFault2))]
        void TestCallFault(int faultNo);

        [Rpc(RpcType.Call)]
        string InvokeCallback(int callbackNo, int p1, string p2);

        [Rpc(RpcType.CallbackMessage)]
        void TestCallbackNotify1(int p1, string p2);

        [Rpc(RpcType.Callback)]
        void TestCallback1(int p1, string p2);

        [Rpc(RpcType.Callback)]
        int TestCallback2(int p1, string p2);

        [Rpc(RpcType.Callback)]
        string TestCallback3(int p1, string p2);
    }

    [MessagePackObject]
    public class TestFault1 : RpcFault
    {
        [Key(1)]
        public string Message { get; set; }

        public override bool Equals(object obj)
        {
            return obj is TestFault1 tf && tf.Message == Message;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }
    }

    [MessagePackObject]
    public class TestFault2 : RpcFault
    {
        [Key(1)]
        public string Message { get; set; }

        public override bool Equals(object obj)
        {
            return obj is TestFault2 tf && tf.Message == Message;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }
    }
}
