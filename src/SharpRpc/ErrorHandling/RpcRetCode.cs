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
        InvalidCredentials,
        ConfigurationError,
        ChannelClosed,
        ConnectionShutdown,
        ConnectionAbortedByPeer,
        ConnectionTimeout,
        LoginTimeout,
        OtherConnectionError,
        SerializationError,
        DeserializationError,
        MessageMarkupError,
        MessageHandlerFailure
    }
}
