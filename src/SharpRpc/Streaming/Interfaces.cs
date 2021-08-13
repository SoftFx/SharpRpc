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

namespace SharpRpc
{
    public interface IStreamMessageFactory<T>
    {
        IStreamPage<T> CreatePage(string streamId);
        //IStreamRxAck CreateAcknowledgement(string streamId);
    }

    public interface IStreamPage : IMessage
    {
        string StreamId { get; }
    }

    public interface IStreamPage<T> : IStreamPage
    {
        List<T> Items { get; set; }
    }

    public interface IStreamRxAck
    {
        string StreamId { get; }
        int ConsumedItems { get; set; }
        int ConsumedBytes { get; set; }
    }
}
