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
    public class LoggerFacade
    {
        private IRpcLogger _extLogger;

        internal void SetExtLogger(IRpcLogger logger)
        {
            _extLogger = logger;
        }

        public void Verbose(string component, string msg)
        {
            if (_extLogger?.VerboseEnabled == true)
                _extLogger.Verbose(component, msg);
        }

        public void Verbose(string component, string format, params object[] formatArgs)
        {
            if (_extLogger?.VerboseEnabled == true)
                _extLogger.Verbose(component, string.Format(format, formatArgs));
        }

        public void Info(string component, string format)
        {
            _extLogger?.Info(component, format);
        }

        public void Info(string component, string format, params object[] formatArgs)
        {
            _extLogger?.Info(component, string.Format(format, formatArgs));
        }

        public void Warn(string component, string msg)
        {
            _extLogger?.Warn(component, msg, null);
        }

        public void Warn(string component, string format, params object[] formatArgs)
        {
            _extLogger?.Warn(component, string.Format(format, formatArgs), null);
        }

        public void Error(string component, string msg)
        {
            _extLogger?.Error(component, msg, null);
        }

        public void Error(string component, Exception ex, string msg)
        {
            _extLogger?.Error(component, msg, ex);
        }
    }
}
