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

        public void Verbose(string component, string format, params string[] formatArgs)
        {
            if (_extLogger?.VerboseEnabled == true)
                _extLogger.Verbose(component, string.Format(format, formatArgs));
        }

        public void Info(string component, string format)
        {
            _extLogger?.Info(component, format);
        }

        public void Info(string component, string format, params string[] formatArgs)
        {
            _extLogger?.Info(component, string.Format(format, formatArgs));
        }

        public void Warn(string component, string msg)
        {
            _extLogger?.Warn(component, msg, null);
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
