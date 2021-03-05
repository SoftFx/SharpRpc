using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    internal class SerializerDeclaration
    {
        public SerializerDeclaration(SerializerBuilderBase builder, TypeString contractTypeName)
        {
            Builder = builder;
            AdapterClassName = new TypeString(contractTypeName.Full + "_" + builder.Name + "_MessageSerializer");
        }

        public TypeString AdapterClassName { get; }
        public SerializerBuilderBase Builder { get; }
    }
}
