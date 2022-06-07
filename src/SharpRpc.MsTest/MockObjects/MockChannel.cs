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
    internal class MockChannel
    {
        public MockChannel()
        {
            Dispatcher = new MockMessageDispatcher(Tx);
        }

        public MockMessageTransmitter Tx { get; } = new MockMessageTransmitter();
        public MockMessageDispatcher Dispatcher { get; }

        public int DropEnqueuedMessages()
        {
            var msgSentCount = 0;
            while (Tx.MessagesToSend.Count > 0)
            {
                Tx.MessagesToSend.Dequeue();
                msgSentCount++;
            }
            return msgSentCount;
        }

        public int SendMessagesTo(MockChannel ch)
        {
            var msgSentCount = 0;
            while (Tx.MessagesToSend.Count > 0)
            {
                var msg = Tx.MessagesToSend.Dequeue();
                ch.Dispatcher.EmulateMessageRx(msg);
                msgSentCount++;
            }
            return msgSentCount;
        }
    }
}
