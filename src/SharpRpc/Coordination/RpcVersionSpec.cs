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

namespace SharpRpc.Coordination
{
    internal class RpcVersionSpec
    {
        public static ShortVersion LatestVersion { get; } = new ShortVersion(0, 0);

        public RpcVersionSpec(ShortVersion version)
        {
            ActualVersion = version;
        }

        public ShortVersion ActualVersion { get; }

        public static RpcVersionSpec TryResolveVersion(ShortVersion clientVersion, out string error)
        {
            //if (clientVersion.Major < LatestVersion.Major)
            //{
            //    error = "The client's protocol version is too low!";
            //    return new RpcVersionSpec(LatestVersion);
            //}
            //else if (clientVersion.Major > LatestVersion.Major)
            //{
            //    error = "The client's protocol version is too high!";
            //    return new RpcVersionSpec(LatestVersion);
            //}

            error = null;

            if (clientVersion >= LatestVersion)
                return new RpcVersionSpec(LatestVersion);
            else
                return new RpcVersionSpec(clientVersion);
        }

        //public bool SupportsLogoutRequest => _actualVersion >= new ShortVersion(0, 0);
    }
}
