using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public interface IRpcLogger
    {
        bool VerboseEnabled { get; }

        void Verbose(string component, string msg);
        void Info(string component, string msg);
        void Warn(string component, string msg, Exception ex);
        void Error(string component, string msg, Exception ex);
    }
}
