// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Disptaching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SharpRpc.MessageDispatcherCore;

namespace SharpRpc.MsTest.MockObjects
{
    internal class MockMessageDispatcher : IDispatcher
    {
        private readonly MessageDispatcherCore _core;

        public MockMessageDispatcher(TxPipeline tx)
        {
            _core = new MessageDispatcherCore(tx, null, OnCoreError);
        }

        //private readonly Dictionary<string, IInteropOperation> _canceledOperations = new Dictionary<string, IInteropOperation>();
        //public IEnumerable<IInteropOperation> CanceledOperations => _canceledOperations.Values;

        public string GenerateOperationId()
        {
            return "C5";
        }

        public RpcResult Register(IDispatcherOperation callObject)
        {
            return _core.TryRegisterOperation(callObject);
        }

        public void Unregister(IDispatcherOperation callObject)
        {
            _core.UnregisterOperation(callObject);
        }

        public void CancelOutgoingCall(object state)
        {
        }

        public void EmulateMessageRx(IMessage msg)
        {
            _core.ProcessMessage(msg);
        }

        public void OnCoreError(RpcRetCode code, string errorMessage)
        {
        }
    }
}
