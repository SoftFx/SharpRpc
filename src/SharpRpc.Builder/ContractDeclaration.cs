using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    internal class ContractDeclaration
    {
        public ContractDeclaration(string typeFullName)
        {
            TypeName = new TypeString(typeFullName);
        }

        public TypeString TypeName { get; }

        public List<CallDeclaration> Calls { get; } = new List<CallDeclaration>();
    }

    internal class CallDeclaration
    {
        public CallDeclaration(string methodName, ContractCallType type)
        {
            MethodName = methodName;
            CallType = type;
        }

        public string MethodName { get; }
        public ContractCallType CallType { get; }
        public bool IsRequestResponceCall => CallType == ContractCallType.ServerCall || CallType == ContractCallType.ClientCall;
        public List<ParamDeclaration> Params { get; } = new List<ParamDeclaration>();
        public ParamDeclaration ReturnParam { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (ReturnParam == null)
                builder.Append("void");
            else
                builder.Append(ReturnParam.ParamType);

            builder.Append(" ").Append(MethodName);
            builder.Append("(");
            builder.Append(string.Join(",", Params.Select(p => p.ParamType)));
            builder.Append(")");

            return builder.ToString();
        }
    }

    internal class ParamDeclaration
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

    internal enum ContractCallType
    {
        ClientCall,
        ClientMessage,
        ServerCall,
        ServerMessage
    }
}
