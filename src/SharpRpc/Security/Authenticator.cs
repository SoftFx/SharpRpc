// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class Authenticator
    {
        public static Authenticator None { get; } = new Null();

#if NET5_0_OR_GREATER
        internal abstract ValueTask<string> OnLogin(ILoginMessage login, SessionContext context);
#else
        internal abstract Task<string> OnLogin(ILoginMessage login, SessionContext context);
#endif
        private class Null : Authenticator
        {
#if NET5_0_OR_GREATER
            internal override ValueTask<string> OnLogin(ILoginMessage login, SessionContext context)
#else
            internal override Task<string> OnLogin(ILoginMessage login, SessionContext context)
#endif
            {
                return FwAdapter.WrappResult((string)null);
            }
        }
    }
}
