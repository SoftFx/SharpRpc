using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public class TypeString
    {
        public TypeString(string typeFullName)
        {
            Full = typeFullName.Trim();
            var nsDelimiterIndex = typeFullName.LastIndexOf(".");

            if (nsDelimiterIndex == 0 || nsDelimiterIndex >= Full.Length - 1)
                throw new Exception();

            if (nsDelimiterIndex > 0)
            {
                Namespace = typeFullName.Substring(0, nsDelimiterIndex);
                Short = typeFullName.Substring(nsDelimiterIndex + 1);
            }
            else
            {
                Namespace = "";
                Short = typeFullName;
            }
        }

        public TypeString(string ns, string name)
        {
            Namespace = ns.Trim();
            Short = name.Trim();
            Full = ns + "." + name;
        }

        public string Namespace { get; }
        public string Short { get; }
        public string Full { get; }
    }
}
