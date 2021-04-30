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
    public interface IRpcSerializer
    {
        void Serialize(IMessage message, MessageWriter writer);
        IMessage Deserialize(MessageReader reader);
    }

    //public interface IMessageSerializer<T>
    //{
    //    void Serialize(T entity, MessageWriter writer);
    //}

    //public interface IMessageDeserializer<T>
    //{
    //    void Deserialize(T entity, MessageReader reader);
    //}
}
