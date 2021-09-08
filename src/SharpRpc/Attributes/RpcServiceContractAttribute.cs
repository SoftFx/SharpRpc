// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace SharpRpc
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class RpcServiceContractAttribute : Attribute
    {
        /// <summary>
        /// Adds means to create preliminary serialized messages. This allows to optimize multicasting
        /// by removing superfluous serilizations of same data. Affects only one-way messages.
        /// </summary>
        public bool EnablePrebuilder { get; set; }
    }
}
