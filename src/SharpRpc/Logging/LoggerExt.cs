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
    internal static class LoggerExt
    {
        public static void Verbose(this IRpcLogger logger, string component, string format, params object[] formatArgs)
        {
            logger.Verbose(component, string.Format(format, formatArgs));
        }

        public static void Info(this IRpcLogger logger, string component, string format, params object[] formatArgs)
        {
            logger.Info(component, string.Format(format, formatArgs));
        }

        public static void Warn(this IRpcLogger logger, string component, string format, params object[] formatArgs)
        {
            logger.Warn(component, string.Format(format, formatArgs), null);
        }

        public static void Error(this IRpcLogger logger, string component, string msg)
        {
            logger.Error(component, msg, null);
        }

        public static void Error(this IRpcLogger logger, string component, Exception ex, string msg)
        {
            logger.Error(component, msg, ex);
        }
    }

    internal class NullLogger : IRpcLogger
    {
        public static NullLogger Instance { get; } =  new NullLogger();

        public bool VerboseEnabled => false;

        public void Error(string component, string msg, Exception ex)
        {
        }

        public void Info(string component, string msg)
        {
        }

        public void Verbose(string component, string msg)
        {
        }

        public void Warn(string component, string msg, Exception ex)
        {
        }
    }
}
