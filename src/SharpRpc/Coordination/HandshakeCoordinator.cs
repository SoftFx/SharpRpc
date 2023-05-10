// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class HandshakeCoordinator
    {
        private readonly HandshakeEncoder _msgEncoder;
        private readonly TimeSpan _timeout;

        public HandshakeCoordinator(int bufferSize, TimeSpan handshakeTimeout)
        {
            _timeout = handshakeTimeout;
            //var maxDomainFieldSize = 2 + HandshakeRequest.MaxDomainChars * 4; //  LEN + UTF8
            //var maxServiceNameFieldSize = 2 + HandshakeRequest.MaxServiceNameChars * 4; // LEN + UTF8
            //var optionsFieldSize = 2; // ushort enum

            //var possibleHandshakeSize = HandshakeRequest.HeaderSize + optionsFieldSize
            //    + maxDomainFieldSize + maxServiceNameFieldSize;

            //if (bufferSize < possibleHandshakeSize)
            //    bufferSize = possibleHandshakeSize;

            _msgEncoder = new HandshakeEncoder(bufferSize);
        }

        public async Task<RpcResult> DoServerSideHandshake(ByteTransport transport)
        {
            using (var timeoutSrc = new CancellationTokenSource(_timeout))
            {
                var rxResult = await TryReceiveRequest(transport, timeoutSrc.Token);

                if (!rxResult.IsOk)
                    return rxResult;

                //var request = rxResult.Value;

                return await TrySend(transport, new HandshakeResponse(), timeoutSrc.Token);
            }
        }

        public async Task<RpcResult> DoClientSideHandshake(ByteTransport transport)
        {
            using (var timeoutSrc = new CancellationTokenSource(_timeout))
            {
                var txResult = await TrySend(transport, new HandshakeRequest(), timeoutSrc.Token);

                if (!txResult.IsOk)
                    return txResult;

                var rxResult = await TryReceiveResponse(transport, timeoutSrc.Token);

                if (!rxResult.IsOk)
                    return rxResult;
            }

            return RpcResult.Ok;
        }

        private Task<RpcResult> TrySend(ByteTransport transport, HandshakeRequest msg, CancellationToken timeoutToken)
        {
            _msgEncoder.Reset();
            msg.WriteTo(_msgEncoder);
            return transport.TrySend(_msgEncoder.GetDataSegment(), timeoutToken);
        }

        private Task<RpcResult> TrySend(ByteTransport transport, HandshakeResponse msg, CancellationToken timeoutToken)
        {
            _msgEncoder.Reset();
            msg.WriteTo(_msgEncoder);
            return transport.TrySend(_msgEncoder.GetDataSegment(), timeoutToken);
        }

        public async Task<RpcResult<HandshakeRequest>> TryReceiveRequest(ByteTransport transport, CancellationToken timeoutToken)
        {
            _msgEncoder.Reset();

            // header 

            var headerRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, HandshakeRequest.HeaderSize, timeoutToken);

            if (!headerRxResult.IsOk)
                return headerRxResult;

            _msgEncoder.Reset(HandshakeRequest.HeaderSize);
            var headerParseResult = HandshakeRequest.TryParseHeader(_msgEncoder, out var pVersion, out var size);

            if (headerParseResult != HandshakeParseResult.Ok)
                return new RpcResult<HandshakeRequest>(RpcRetCode.InvalidHandshake, "TO DO");

            // body

            var bodyRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, size, timeoutToken);

            if (!bodyRxResult.IsOk)
                return bodyRxResult;

            _msgEncoder.Reset(size);
            if (!HandshakeRequest.TryParseBody(_msgEncoder, pVersion, out var request))
                return new RpcResult(RpcRetCode.InvalidHandshake, "TO DO");

            return RpcResult.FromResult(request);
        }

        public async Task<RpcResult<HandshakeResponse>> TryReceiveResponse(ByteTransport transport, CancellationToken timeoutToken)
        {
            _msgEncoder.Reset();

            // header 

            var headerRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, HandshakeRequest.HeaderSize, timeoutToken);

            if (!headerRxResult.IsOk)
                return headerRxResult;

            _msgEncoder.Reset(HandshakeRequest.HeaderSize);
            var headerParseResult = HandshakeResponse.TryParseHeader(_msgEncoder, out var pVersion, out var size);

            if (headerParseResult != HandshakeParseResult.Ok)
                return new RpcResult<HandshakeResponse>(RpcRetCode.InvalidHandshake, "TO DO");

            // body

            var bodyRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, size, timeoutToken);

            if (!bodyRxResult.IsOk)
                return bodyRxResult;

            _msgEncoder.Reset(size);
            if (!HandshakeResponse.TryParseBody(_msgEncoder, pVersion, out var request))
                return new RpcResult(RpcRetCode.InvalidHandshake, "TO DO");

            return RpcResult.FromResult(request);
        }
    }

    [Flags]
    public enum RpcSessionOptions : ushort
    {
        None = 0,
    }

    public enum HandshakeResultCode
    {
        Accepted = 0,
        VersionIncompatibility = 1,
        OtherIncompatibility = 100
    }

    public struct ProtocolVersion
    {
        public ProtocolVersion(byte major, byte minor)
        {
            Major = major;
            Minor = minor;
        }

        public byte Major { get; set; }
        public byte Minor { get; set; }
    }
}
