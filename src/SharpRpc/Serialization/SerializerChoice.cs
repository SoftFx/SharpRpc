using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public enum SerializerChoice
    {
        DataContract        = 0,
        MessagePack         = 1,
        ProtobufNet         = 2
    }
}
