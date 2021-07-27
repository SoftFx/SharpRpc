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

namespace SharpRpc.Config
{
    public abstract class EndpointConfigElement
    {
        internal EndpointConfigElement(IConfigHost host)
        {
            Host = host;
        }

        internal IConfigHost Host { get; }

        protected void ThrowIfImmutable()
        {
            Host.ThrowIfImmutable();
        }

        internal void Validate()
        {
            ValidateConfiguration();
        }

        protected virtual void ValidateConfiguration()
        {
        }
    }

    internal interface IConfigHost
    {
        object SyncObject { get; }

        void ThrowIfImmutable();
    }
}
