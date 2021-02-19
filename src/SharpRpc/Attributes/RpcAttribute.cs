using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcAttribute : Attribute
    {
        public RpcAttribute(RpcType type)
        {
            Type = type;
        }

        public RpcType Type { get;  }
    }


    public enum RpcType
    {
        ClientCall          = 0,
        ClientMessage       = 1,
        ServerCall          = 2,
        ServerMessage       = 3
    }
}
