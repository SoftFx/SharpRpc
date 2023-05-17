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

namespace SharpRpc
{
    internal struct Log
    {
        public Log(string logId, IRpcLogger logger)
        {
            LogId = logId;
            Logger = logger;
        }

        public string LogId { get; }
        public IRpcLogger Logger { get; }

        public bool VerboseEnabled => Logger.VerboseEnabled;
        public bool InfoEnabled => Logger.InfoEnabled;

        public void Verbose(string msg) => Logger.Verbose(LogId, msg);
        public void Info(string msg) => Logger.Verbose(LogId, msg);
        public void Warn(string msg, Exception ex) => Logger.Verbose(LogId, msg, ex);
        public void Error(string msg, Exception ex) => Logger.Verbose(LogId, msg, ex);
    }
}
