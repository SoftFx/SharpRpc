using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    internal class SerializerDeclaration
    {
        public SerializerDeclaration(SerializerBuilderBase builder, TypeString facadeClassName)
        {
            Builder = builder;
            AdapterClassName = new TypeString(facadeClassName.Full, builder.Name + "Adapter");
        }

        public TypeString AdapterClassName { get; }
        public SerializerBuilderBase Builder { get; }
    }
}
