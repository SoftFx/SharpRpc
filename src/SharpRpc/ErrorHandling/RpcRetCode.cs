using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public enum RpcRetCode
    {
        Ok,
        OtherError,
        UnknownError,
        ProtocolViolation,
        InvalidChannelState,
        ConfigurationError,
        ChannelClosed,
        ConnectionShutdown,
        ConnectionAbortedByPeer,
        ConnectionTimeout,
        OtherConnectionError,
        SerializationError,
        DeserializationError,
        MessageMarkupError,
        MessageHandlerFailure
    }
}
