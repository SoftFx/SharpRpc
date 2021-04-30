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
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcAttribute : Attribute
    {
        public RpcAttribute(RpcType type)
        {
            Type = type;
        }

        public RpcType Type { get;  }
    }


    public enum RpcType
    {
        ClientCall          = 0,
        ClientMessage       = 1,
        ServerCall          = 2,
        ServerMessage       = 3
    }
}
