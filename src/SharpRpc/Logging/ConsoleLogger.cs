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
    public class ConsoleLogger : IRpcLogger
    {
        public ConsoleLogger()
        {
        }

        public bool IsVerboseEnabled { get; set; } = false;
        public bool IsInfoEnabled { get; set; } = true;
        public bool IsMessageLoggingEnabled { get; set; } = false;
        public bool IsAuxMessageLoggingEnabled { get; set; } = false;
        public bool PrintStackTrace { get; set; }

        public void Info(string component, string msg)
        {
            Console.WriteLine(component + " " + msg);
        }

        public void Verbose(string component, string msg)
        {
            Console.WriteLine(component + " " + msg);
        }

        public void Warn(string component, string msg, Exception ex)
        {
            Console.WriteLine(component + " " + msg);
            if (ex != null && PrintStackTrace)
                Console.WriteLine(ex.ToString());
        }

        public void Error(string component, string msg, Exception ex)
        {
            Console.Error.WriteLine(component + " " + msg);
            if (ex != null && PrintStackTrace)
                Console.WriteLine(ex.ToString());
        }
    }
}
