﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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
    [RpcServiceContract(GeneratePostResponseServiceMethods = true)]
    [RpcSerializer(SerializerChoice.MessagePack)]
    interface FunctionTestContract
    {
        [RpcContract(0, RpcType.Message)]
        void TestNotify1(int p1, string p2);

        [RpcContract(1, RpcType.Call)]
        void TestCall1(int p1, string p2);

        [RpcContract(2, RpcType.Call)]
        string TestCall2(int p1, string p2);

        [RpcContract(3, RpcType.Call)]
        string TestCall3(FooData data);

        [RpcContract(4, RpcType.Call)]
        string TestCrash(int p1, string p2);

        [RpcContract(5, RpcType.Call)]
        string TestRpcException(int p1, string p2);

        [RpcContract(6, RpcType.Call)]
        [RpcFault(0, typeof(TestFault1))]
        [RpcFault(1, typeof(TestFault2))]
        void TestCallFault(int faultNo);

        [RpcContract(7, RpcType.Call)]
        string InvokeCallback(int callbackNo, int p1, string p2);

        [RpcContract(8, RpcType.CallbackMessage)]
        void TestCallbackNotify1(int p1, string p2);

        [RpcContract(9, RpcType.Callback)]
        void TestCallback1(int p1, string p2);

        [RpcContract(10, RpcType.Callback)]
        int TestCallback2(int p1, string p2);

        [RpcContract(11, RpcType.Callback)]
        string TestCallback3(int p1, string p2);

        [RpcContract(12, RpcType.Call)]
        List<Tuple<int>> ComplexTypesCall(List<DateTime> list, List<List<DateTime>> listOfLists, Dictionary<int, int> dictionary);

        [RpcContract(13, RpcType.Call)]
        [RpcStreamOutput(typeof(int))]
        StreamCallResult TestOutStream(TimeSpan delay, int count, StreamTestOptions options);

        [RpcContract(14, RpcType.Call)]
        [RpcStreamInput(typeof(int))]
        StreamCallResult TestInStream(TimeSpan delay, StreamTestOptions options);

        [RpcContract(15, RpcType.Call)]
        [RpcStreamInput(typeof(int))]
        [RpcStreamOutput(typeof(int))]
        int TestDuplexStream(TimeSpan delay, StreamTestOptions options);

        [RpcContract(16, RpcType.Call)]
        bool CancellableCall(TimeSpan delay);

        [RpcContract(17, RpcType.Call)]
        string GetSessionSharedProperty(string name);

        [RpcContract(18, RpcType.Call)]
        [RpcStreamOutput(typeof(byte))]
        StreamCallResult TestOutBinStream(string fileName, StreamTestOptions options, StreamWriteOptions writeMode);

        [RpcContract(19, RpcType.Call)]
        void DropSession();

        [RpcContract(20, RpcType.Call)]
        void BrokenRequest(BrokenEntity requets);

        [RpcContract(21, RpcType.Call)]
        BrokenEntity BrokenResponse();

        [RpcContract(22, RpcType.Call)]
        [RpcStreamOutput(typeof(BrokenEntity))]
        void BrokenOutputStream();
    }

    public enum StreamTestOptions
    {
        None,
        JustExit,
        InvokeCompletion,
        DoNotInvokeCompletion,
        ImmediateFault,
        ImmediateCustomFault,
        FollowingFault,
        FollowingCustomFault
    }

    public enum StreamReadOptions
    {
        OneByOne,
        Bulk,
        Paged
    }

    public enum StreamWriteOptions
    {
        OneByOne,
        Bulk,
        BulkStartCommit,
    }

    [MessagePackObject]
    public struct StreamCallResult
    {
        [SerializationConstructor]
        public StreamCallResult(StreamCallExitCode code, int sum)
        {
            ExitCode = code;
            ItemSum = sum;
        }

        [Key(0)]
        public StreamCallExitCode ExitCode { get; }

        [Key(1)]
        public int ItemSum { get; }
    }

    public enum StreamCallExitCode
    {
        ImmediateExit,
        StreamCompleted,
        StreamWriteCancelled,
        Error
    }

    [MessagePackObject]
    public class TestFault1
    {
        [Key(1)]
        public int CustomCode { get; set; }

        public override bool Equals(object obj)
        {
            return obj is TestFault1 tf && tf.CustomCode == CustomCode;
        }

        public override int GetHashCode()
        {
            return CustomCode.GetHashCode();
        }
    }

    [MessagePackObject]
    public class TestFault2
    {
        [Key(1)]
        public int CustomCode { get; set; }

        public override bool Equals(object obj)
        {
            return obj is TestFault2 tf && tf.CustomCode == CustomCode;
        }

        public override int GetHashCode()
        {
            return CustomCode.GetHashCode();
        }
    }

    [MessagePackObject]
    public class FooData
    {
        [Key(1)]
        public string Name { get; set; }

        [Key(2)]
        public int Age { get; set; }

        [Key(3)]
        public List<FooData> Relatives { get; set; }
    }

    [MessagePackObject]
    public class BrokenEntity
    {
        [Key(1)]
        public string Id { get; set; }

        public string Name { get; set; }
    }
}
