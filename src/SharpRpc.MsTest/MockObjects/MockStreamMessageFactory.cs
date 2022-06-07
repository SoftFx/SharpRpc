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
        public IStreamCompletionMessage CreateCompletionMessage(string streamId) => new MockStreamCompletionMessage(streamId);
        public IStreamCompletionRequestMessage CreateCompletionRequestMessage(string streamId) => new MockStreamCompletionRequestMessage(streamId);
        public IStreamPage<T> CreatePage(string streamId) => new MockStreamPageMessage<T>(streamId);
        public IStreamPageAck CreatePageAcknowledgement(string streamId) => new MockStreamPageAck(streamId);
    }

    internal class MockStreamCompletionMessage : IStreamCompletionMessage
    {
        public MockStreamCompletionMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
    }

    internal class MockStreamCompletionRequestMessage : IStreamCompletionRequestMessage
    {
        public MockStreamCompletionRequestMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
    }

    internal class MockStreamPageMessage<T> : IStreamPage<T>, ICloneOnSendMessage
    {
        public MockStreamPageMessage(string callId)
        {
            CallId = callId;
        }

        public string CallId { get; set; }
        public List<T> Items { get; set; }

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
    }


    interface ICloneOnSendMessage
    {
        IMessage Clone();
    }
}
