// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRpc.MsTest.MockObjects;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.MsTest
{
    [TestClass]
    public class StreamTest
    {
        public static readonly TimeSpan TaskTimeout = TimeSpan.FromMilliseconds(100);

        [TestMethod]
        public void OutputStreamCall_CancelByClinet()
        {
            SetupOuputCall<int>(out var clientCallObj, out var clientCh, out var serverCallObj, out var serverCh);

            // skip request message
            var msgSkipped = clientCh.DropEnqueuedMessages();
            Assert.AreEqual(1, msgSkipped);

            var cancelReadSrc = new CancellationTokenSource();
            var items = new List<int>();
            var readTask = ReadStream(clientCallObj.OutputStream, items, cancelReadSrc.Token);

            for (int i = 0; i < 4; i++)
                serverCallObj.OutputStream.WriteAsync(i);

            var msgSent1 = serverCh.SendMessagesTo(clientCh);
            Assert.AreEqual(4, msgSent1);

            var msgSent2 = clientCh.SendMessagesTo(serverCh);
            Assert.AreEqual(4, msgSent2);

            cancelReadSrc.Cancel();

            var msgSent3 = clientCh.SendMessagesTo(serverCh);
            Assert.AreEqual(1, msgSent3);

            var msgSent4 = serverCh.SendMessagesTo(clientCh);
            Assert.AreEqual(1, msgSent4);

            if (!readTask.Wait(TaskTimeout))
                Assert.Fail("Read loop is still running!");

            Assert.AreEqual(4, items.Count);
        }

        private async Task ReadStream<T>(StreamReader<T> stream, List<T> toContainer, CancellationToken cToken)
        {
            var e = stream.GetEnumerator(cToken);

            while (await e.MoveNextAsync())
                toContainer.Add(e.Current);
        }

        private void SetupOuputCall<TItem>(out StreamCall<object, TItem, object> clientCallObj, out MockChannel clientCh,
            out ServiceStreamingCallContext<object, TItem> serverCallObj, out MockChannel serverCh)
        {
            var openRequest = new MockOpenStreamRequest(true);

            clientCh = new MockChannel();
            serverCh = new MockChannel();

            var msgFactory = new MockStreamMessageFactory<TItem>();

            clientCallObj = new StreamCall<object, TItem, object>(openRequest, null, new StreamOptions(openRequest), clientCh.Tx, clientCh.Dispatcher,
                null, msgFactory, false, CancellationToken.None);

            serverCallObj = new ServiceStreamingCallContext<object, TItem>(openRequest, serverCh.Tx, serverCh.Dispatcher, null, msgFactory);
        }
    }
}
