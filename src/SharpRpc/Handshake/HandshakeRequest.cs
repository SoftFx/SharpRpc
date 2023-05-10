// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Security.Cryptography;

namespace SharpRpc
{
    internal class HandshakeRequest
    {
        public static readonly byte[] RpcToken = new byte[] { (byte)'#', (byte)'R', (byte)'P', (byte)'C' };
        public static readonly int HeaderSize = 4 + 2 + 2; // Token + Version + Size

        public const int MaxDomainChars = 253;
        public const int MaxServiceNameChars = 128;

        // *** header ***

        // 1 - #RPC (4 bytes token)
        // 2 - protocol version (2 bytes)
        // 3 - Body size (2 bytes)

        // *** body ***

        // 4 - Options (2 bytes, for future use)
        // 5 - Domain (LEN UTF8)
        // 6 - Service name (LEN UTF8)
        // 7* - may contain unknown fields (compatibility)

        public ProtocolVersion RpcVersion { get; set; }
        public RpcSessionOptions Options { get; set; }
        public string Domain { get; set; }
        public string ServiceName { get; set; }

        public void WriteTo(HandshakeEncoder encoder)
        {
            encoder.Write(RpcToken);
            encoder.Write(RpcVersion);
            var lengthFieldPos = encoder.Position;
            encoder.Write((ushort)0); // empty size field
            encoder.Write((ushort)Options);
            encoder.Write(Domain ?? "dd"); // string.Empty);
            encoder.Write(ServiceName ?? "name name");// string.Empty);

            // update size field
            var size = encoder.Length - lengthFieldPos - 2;
            encoder.SetPosition(lengthFieldPos);
            encoder.Write((ushort)size);
        }

        public static HandshakeParseResult TryParseHeader(HandshakeEncoder encoder, out ProtocolVersion version, out ushort size)
        {
            version = default;
            size = default;

            if (!encoder.TryReadByteArray(4, out var tokenBytes))
                throw new Exception("Invalid header size!");

            for (int i = 0; i < 4; i++)
            {
#if NET5_0_OR_GREATER
                if (tokenBytes[i] != RpcToken[i])
#else
                if (tokenBytes.Array[i + tokenBytes.Offset] != RpcToken[i])
#endif
                    return HandshakeParseResult.InvalidToken;
            }

            if (!encoder.TryReadVersion(out version))
                throw new Exception("Invalid header size!");

            if (!encoder.TryReadUInt16(out size))
                throw new Exception("Invalid header size!");

            return HandshakeParseResult.Ok;
        }

        public static bool TryParseBody(HandshakeEncoder encoder, ProtocolVersion version, out HandshakeRequest request)
        {
            request = null;

            if (!encoder.TryReadUInt16(out var rawOptions))
                return false;

            if (!encoder.TryReadString(out var domain))
                return false;

            if (!encoder.TryReadString(out var serviceName))
                return false;

            request = new HandshakeRequest()
            {
                RpcVersion = version,
                Options = (RpcSessionOptions)rawOptions,
                Domain = domain,
                ServiceName = serviceName,
            };

            return true;
        }
    }

    public enum HandshakeParseResult
    {
        Ok,
        InvalidToken,
    }
}
