using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public abstract class SerializerBuilderBase
    {
        public abstract void BuildMessageSerializer(MessageBuilder builder);
    }
}
