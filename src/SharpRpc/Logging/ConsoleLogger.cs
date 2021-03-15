using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public class ConsoleLogger : IRpcLogger
    {
        private bool _printStack;

        public ConsoleLogger(bool verbose, bool printStackTrace)
        {
            VerboseEnabled = verbose;
            _printStack = printStackTrace;
        }

        public bool VerboseEnabled { get; }

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
            if (ex != null && _printStack)
                Console.WriteLine(ex.ToString());
        }

        public void Error(string component, string msg, Exception ex)
        {
            Console.Error.WriteLine(component + " " + msg);
            if (ex != null && _printStack)
                Console.WriteLine(ex.ToString());
        }
    }
}
