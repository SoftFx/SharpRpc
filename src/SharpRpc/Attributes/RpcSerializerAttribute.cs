using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public class RpcSerializerAttribute : Attribute
    {
        public RpcSerializerAttribute(SerializerChoice choice)
        {
            Choice = choice;
        }

        public SerializerChoice Choice { get; }
    }
}
