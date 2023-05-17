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

namespace SharpRpc.Server
{
    //internal struct ServiceKey
    //{
    //    public ServiceKey(string domain, string serviceName)
    //    {
    //        Domain = domain;
    //        ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
    //    }

    //    public string ServiceName { get; }
    //    public string Domain { get; }

    //    public override bool Equals(object obj)
    //    {
    //        if (obj is ServiceKey otherKey)
    //            return otherKey.Domain == Domain && otherKey.ServiceName == ServiceName;

    //        return false;
    //    }

    //    public override int GetHashCode()
    //    {
    //        unchecked
    //        {
    //            int result = Domain?.GetHashCode() ?? 0;
    //            result = (result * 397) ^ ServiceName.GetHashCode();
    //            return result;
    //        }
    //    }
    //}
}
