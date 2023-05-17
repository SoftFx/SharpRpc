// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace SharpRpc
{
    internal class HandshakeResponse
    {
        // *** header ***

        // 1 - #RPC (4 bytes token)
        // 2 - version (2 bytes)
        // 3 - Further message size (2 bytes, not including token, version and size fields)

        // *** body ***

        // 4 - Options (2 bytes, for future use)


        public ShortVersion RpcVersion { get; set; }
        public RpcSessionOptions Options { get; set; }
        public HandshakeResultCode RetCode { get; set; }

        public void WriteTo(HandshakeEncoder encoder)
        {
            encoder.Write(HandshakeRequest.RpcToken);
            encoder.Write(RpcVersion);
            var lengthFieldPos = encoder.Position;
            encoder.Write((ushort)0); // empty size field

            encoder.Write((ushort)Options);
            encoder.Write((ushort)RetCode);

            // update size field
            var size = encoder.Length - lengthFieldPos - 2;
            encoder.SetPosition(lengthFieldPos);
            encoder.Write((ushort)size);
        }

        public static HandshakeParseResult TryParseHeader(HandshakeEncoder encoder, out ShortVersion version, out ushort size)
            => HandshakeRequest.TryParseHeader(encoder, out version, out size);

        public static bool TryParseBody(HandshakeEncoder encoder, ShortVersion version, out HandshakeResponse response)
        {
            response = null;

            if (!encoder.TryReadUInt16(out var rawOptions))
                return false;

            if (!encoder.TryReadUInt16(out var rawRetCode))
                return false;

            response = new HandshakeResponse()
            {
                RpcVersion = version,
                Options = (RpcSessionOptions)rawOptions,
                RetCode = (HandshakeResultCode)rawRetCode,
            };

            return true;
        }
    }
}
