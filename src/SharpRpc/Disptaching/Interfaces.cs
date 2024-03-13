// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace SharpRpc.Disptaching
{
    internal interface IDispatcher
    {
        string GenerateOperationId();
        IRpcLogger Logger { get; }

        RpcResult Register(IDispatcherOperation callObject);
        void Unregister(IDispatcherOperation callObject);
        void CancelOutgoingCall(object state);
    }

    internal interface IDispatcherOperation
    {
        string CallId { get; }
        IRequestMessage RequestMessage { get; }
        RpcResult OnUpdate(IInteropMessage message);

        // Closes the communication object instantly without any further messaging.
        // This method is typically called in case of connection loss.
        void Terminate(RpcResult fault);

        // *** initiator-side methods ***

        void OnRequestCancelled();
        RpcResult OnResponse(IResponseMessage respMessage);
        void OnFault(RpcResult result);
        void OnFaultResponse(IRequestFaultMessage faultMessage);
    }
}
