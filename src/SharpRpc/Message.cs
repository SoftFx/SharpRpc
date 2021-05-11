// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpRpc
{
    public interface IMessage
    {
    }

    public interface IRequest : IMessage
    {
        string CallId { get; set; }
        //int? FromRecipient { get; set; }
    }

    public interface IResponse : IMessage
    {
        string CallId { get; set; }
        //int? ToRecipient { get; }
    }

    public interface IResponse<T> : IResponse
    {
        T Result { get; }
    }

    public interface ISystemMessage : IMessage
    {
    }

    public interface MessageWriter
    {
        IBufferWriter<byte> ByteBuffer { get; } 
        Stream ByteStream { get; }
    }

    public interface MessageReader
    {
        ReadOnlySequence<byte> ByteBuffer { get; }
        Stream ByteStream { get; }
    }
}
