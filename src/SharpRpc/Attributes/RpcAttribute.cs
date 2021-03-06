﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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

        /// <summary>
        /// Specifies both call destination (cliet or server) and call type (one way message or request-responce).
        /// </summary>
        public RpcType Type { get;  }

        /// <summary>
        /// Adds means to create preliminary serialized message. This allows to optimize multicasting
        /// by removing superfluous serilizations of same data. Affects only one-way messages.
        /// </summary>
        public bool EnablePrebuild { get; set; }
    }

    public enum RpcType
    {
        /// <summary>
        /// One-way message from client to server.
        /// </summary>
        Message = 1,

        /// <summary>
        /// Remote call from client to server.
        /// </summary>
        Call = 2,

        /// <summary>
        /// Remote call from server to client.
        /// </summary>
        Callback = 3,

        /// <summary>
        /// One-way message from server to client.
        /// </summary>
        CallbackMessage = 4
    }
}
