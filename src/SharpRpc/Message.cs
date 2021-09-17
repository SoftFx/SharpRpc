// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    public interface IMessage
    {
    }

    public interface IPrebuiltMessage : IMessage
    {
        void WriteTo(ushort serializedId, MessageWriter writer);
    }

    public interface IInteropMessage : IMessage
    {
        string CallId { get; set; }
    }

    public interface IRequestMessage : IInteropMessage
    {
        RequestOptions Options { get; set; }
    }

    [Flags]
    public enum RequestOptions : byte
    {
        None = 0,
        CancellationEnabled = 1
    }

    public interface IResponseMessage : IInteropMessage
    {
    }

    public interface IResponseMessage<T> : IResponseMessage
    {
        T Result { get; }
    }

    public interface ICancelRequestMessage : IInteropMessage
    {
    }

    public interface ICancelStreamingMessage : IInteropMessage
    {
    }

    public interface IRequestFaultMessage : IResponseMessage
    {
        string Text { get; set; }
        RequestFaultCode Code { get; set; }
        ICustomFaultBinding GetCustomFaultBinding();
    }

    public interface ISystemMessage : IMessage
    {
    }

    public interface ICustomFaultBinding
    {
        object GetFault();
        RpcFaultException CreateException(string message);
    }

    public interface MessageWriter
    {
#if NET5_0_OR_GREATER
        System.Buffers.IBufferWriter<byte> ByteBuffer { get; }
#endif
        Stream ByteStream { get; }
    }

    public interface MessageReader
    {
#if NET5_0_OR_GREATER
        System.Buffers.ReadOnlySequence<byte> ByteBuffer { get; }
#endif
        Stream ByteStream { get; }
    }
}
