// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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


        public static void LogFault(this IRpcLogger logger, string component, Exception ex, [CallerMemberName] string methodName = null)
        {
            logger.Error(component, $"{methodName}() failed!", ex);
        }

        public static Task LogFault(this Task task, IRpcLogger logger, string component, [CallerMemberName] string methodName = null)
        {
            return task.OnFaulted(ex => logger.Error(component, $"{methodName}() failed!", ex));
        }
    }

    internal class NullLogger : IRpcLogger
    {
        public static NullLogger Instance { get; } =  new NullLogger();

        public bool IsVerboseEnabled => false;
        public bool IsInfoEnabled => false;
        public bool IsMessageLoggingEnabled => false;
        public bool IsAuxMessageLoggingEnabled => false;

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
