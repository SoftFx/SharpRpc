// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public enum RpcRetCode : ushort
    {
        Ok = 0,
        OtherError = 1,
        UnknownError = 2,

        ProtocolViolation = 10,
        InvalidChannelState = 11,
        InvalidCredentials = 12,
        ConfigurationError = 13,
        ChannelClosed = 14,
        ChannelClosedByOtherSide = 15,
        ConnectionShutdown = 16,
        ConnectionAbortedByPeer = 17,
        ConnectionTimeout = 18,
        LoginTimeout = 19,
        SecurityError = 20,
        OtherConnectionError = 21,
        SerializationError = 22,
        DeserializationError = 23,
        MessageMarkupError = 24,
        UnexpectedMessage = 25,
        OperationCanceled = 26,
        InvalidHandshake = 27,
        UnknownService = 28,
        UnknownHostName = 29,
        UnsupportedProtocolVersion = 30,
        ChannelOpenEventFailed = 31,
        HostNotFound = 32,
        HostUnreachable = 33,
        ConnectionRefused = 34,

        RequestFault = 50,

        // Unexpected exception in request handler (not covered by fault contract)
        RequestCrash = 51,

        // Exception in a message handler (one-way handlers should not throw exceptions)
        MessageHandlerCrash = 52,

        // The stream was completed for additions and does not accept new records
        StreamCompleted = 53,

        // Exception in an event handler (event handlers should not throw exceptions)
        EventHandlerCrash = 54,

        // Exception in the Init method (one-way handlers should not throw exceptions)
        InitHanderCrash = 55
    }
}
