using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public class ParamDeclaration
    {
        public ParamDeclaration(int index, string type, string name = null)
        {
            Index = index;
            ParamType = type;
            ParamName = name;
            MessagePropertyName = "Arg" + index;
        }

        public string ParamType { get; }
        public string ParamName { get; }
        public int Index { get; }
        public string MessagePropertyName { get; }
    }
}
