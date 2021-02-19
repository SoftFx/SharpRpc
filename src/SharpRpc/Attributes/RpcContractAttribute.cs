using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class RpcContractAttribute : Attribute
    {
    }
}
