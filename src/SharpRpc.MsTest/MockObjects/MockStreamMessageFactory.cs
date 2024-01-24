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

namespace SharpRpc.MsTest.MockObjects
{
    internal class MockStreamMessageFactory<T> : IStreamMessageFactory<T>
    {
        public IStreamCancelMessage CreateCancelMessage(string streamId) => new MockStreamCancelMessage(streamId);
        public IStreamPage<T> CreatePage(string streamId) => new MockStreamPageMessage<T>(streamId);
        public IStreamPageAck CreatePageAcknowledgement(string streamId) => new MockStreamPageAck(streamId);
        public IStreamCloseMessage CreateCloseMessage(string streamId) => new MockStreamCloseMessage(streamId);
        public IStreamCloseAckMessage CreateCloseAcknowledgement(string streamId) => new MockStreamCloseAckMessage(streamId);
    }

    internal class MockStreamCloseMessage : IStreamCloseMessage
    {
        public MockStreamCloseMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
        public StreamCloseOptions Options { get; set; }
        public string ContractMessageName => "StreamClose";
    }

    internal class MockStreamCloseAckMessage : IStreamCloseAckMessage
    {
        public MockStreamCloseAckMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
        public string ContractMessageName => "CloseAck";
    }

    internal class MockStreamCancelMessage : IStreamCancelMessage
    {
        public MockStreamCancelMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
        public StreamCancelOptions Options { get; set; }
        public string ContractMessageName => "StreamCancel";
    }

    internal class MockStreamPageMessage<T> : IStreamPage<T>, ICloneOnSendMessage
    {
        public MockStreamPageMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
        public List<T> Items { get; set; }
        public string ContractMessageName => "StreamPage";

        public IMessage Clone()
        {
            return new MockStreamPageMessage<T>(CallId) { Items = Items.ToList() };
        }
    }

    internal class MockStreamPageAck : IStreamPageAck
    {
        public MockStreamPageAck(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
        public ushort Consumed { get; set; }
        public string ContractMessageName => "StreamPageAck";
    }

    interface ICloneOnSendMessage
    {
        IMessage Clone();
    }
}
