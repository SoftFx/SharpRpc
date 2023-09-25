// Copyright © 2022 Soft-Fx. All rights reserved.
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

namespace SharpRpc.Serialization
{
    internal class SimplifiedEncoder
    {
    }

    internal interface ISegmetedBufferEnumerator
    {
        byte[] Page { get; }
        int PageSize { get; }
        int PageOffset { get; }
        int PageIndex { get; }

        void Advance(int value);
    }
}
