﻿// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Tcp
{
    public class TcpConnectionInfo : TransportInfo
    {
        internal TcpConnectionInfo(IPEndPoint remEp, IPEndPoint localEp)
        {
            RemoteEndPoint = remEp;
            LocalEndPoint = localEp;
        }

        public IPEndPoint RemoteEndPoint { get; }
        public IPEndPoint LocalEndPoint { get; }

        internal override void DumptTo(Log log)
        {
            if (log.InfoEnabled)
                log.Info($"RemoteEndPoint={RemoteEndPoint}, LocalEndPoint={LocalEndPoint}");
        }
    }
}
