// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Coordination;
using SharpRpc.Server;
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

        public async Task<HandshakeResult> DoServerSideHandshake(ByteTransport transport, ServiceRegistry services, Log parentLog)
        {
            using (var timeoutSrc = new CancellationTokenSource(_timeout))
            {
                var rxResult = await TryReceiveRequest(transport, timeoutSrc.Token).ConfigureAwait(false);

                if (!rxResult.IsOk)
                {
                    if (parentLog.InfoEnabled)
                        parentLog.Info("Failed to receive handshake request! " + rxResult.FaultMessage);
                    return default;
                }

                var request = rxResult.Value;
                var clientVersion = request.RpcVersion;

                if (parentLog.InfoEnabled)
                    parentLog.Info($"Incoming handshake, client v{clientVersion.Minor}.{clientVersion.Major}," +
                        $" target={request.HostName}/{request.ServiceName}");

                var retCode = HandshakeResultCode.Accepted;

                var versionSpec = RpcVersionSpec.TryResolveVersion(clientVersion, out var resolveError);

                if (resolveError != null)
                {
                    retCode = HandshakeResultCode.VersionIncompatibility;
                    parentLog.Info(resolveError);
                }

                var resolveRetCode = services.TryResolve(request.HostName, request.ServiceName, out var service);
                if (resolveRetCode != ServiceRegistry.ResolveRetCode.Ok)
                {
                    if (resolveRetCode == ServiceRegistry.ResolveRetCode.ServiceNotFound)
                        retCode = HandshakeResultCode.UnknwonService;
                    else
                        retCode = HandshakeResultCode.UnknownHostName;
                }

                //var actualVersion = clientVersion;

                var response = new HandshakeResponse();
                response.RpcVersion = versionSpec?.ActualVersion ?? RpcVersionSpec.LatestVersion;
                response.RetCode = retCode;

                if (parentLog.InfoEnabled)
                    parentLog.Info($"Outgoing handshake, v{response.RpcVersion.Major}.{response.RpcVersion.Minor}," +
                        $" code={retCode}");

                var sendResult = await TrySend(transport, response, timeoutSrc.Token).ConfigureAwait(false);

                if (!sendResult.IsOk)
                {
                    if (parentLog.InfoEnabled)
                        parentLog.Info("Failed to send handshake response! " + rxResult.FaultMessage);
                    return default;
                }

                if (retCode == HandshakeResultCode.Accepted)
                    return new HandshakeResult(service, versionSpec);

                return default;
            }
        }

        public async Task<RpcResult> DoClientSideHandshake(ByteTransport transport, CancellationToken cancelToken, string hostName, string serviceName)
        {
            hostName = hostName?.Trim().ToLowerInvariant();
            serviceName = serviceName?.Trim().ToLowerInvariant();

            using (var timeoutSrc = new CancellationTokenSource(_timeout))
            using (cancelToken.Register(timeoutSrc.Cancel))
            {
                var request = new HandshakeRequest();
                request.RpcVersion = new ShortVersion(0, 0);
                request.ServiceName = serviceName;
                request.HostName = hostName;

                var txResult = await TrySend(transport, request, timeoutSrc.Token).ConfigureAwait(false);

                if (!txResult.IsOk)
                    return txResult;

                var rxResult = await TryReceiveResponse(transport, timeoutSrc.Token).ConfigureAwait(false);

                if (!rxResult.IsOk)
                    return rxResult;

                var clientVersion = request.RpcVersion;
                var serverRpcVersion = rxResult.Value.RpcVersion;

                switch (rxResult.Value.RetCode)
                {
                    case HandshakeResultCode.Accepted:
                        return RpcResult.Ok;
                    case HandshakeResultCode.UnknwonService:
                        return new RpcResult(RpcRetCode.UnknownService,
                        $"Specified service ('{serviceName}') is not found! Please check the service name.");
                    case HandshakeResultCode.UnknownHostName:
                        return new RpcResult(RpcRetCode.UnknownHostName,
                        $"The server has not accepted the specified hostname ('{hostName}')!");
                    case HandshakeResultCode.VersionIncompatibility:
                        return new RpcResult(RpcRetCode.UnsupportedProtocolVersion,
                            $"The server has not accepted the client's protocol version ({clientVersion.Major}.{clientVersion.Minor})!");
                    default:
                        return new RpcResult(RpcRetCode.UnknownError,
                            "The server has not accepted the connection but has not provided any meaningful error code!");
                }

                //return HandshakeRetCodeToTcpResult(rxResult.Value.RetCode);
            }
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

            var headerRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, HandshakeRequest.HeaderSize, timeoutToken).ConfigureAwait(false);

            if (!headerRxResult.IsOk)
                return headerRxResult;

            _msgEncoder.Reset(HandshakeRequest.HeaderSize);
            var headerParseResult = HandshakeRequest.TryParseHeader(_msgEncoder, out var pVersion, out var size);

            if (headerParseResult != HandshakeParseResult.Ok)
                return new RpcResult<HandshakeRequest>(RpcRetCode.InvalidHandshake, "TO DO");

            // body

            var bodyRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, size, timeoutToken).ConfigureAwait(false);

            if (!bodyRxResult.IsOk)
                return bodyRxResult;

            _msgEncoder.Reset(size);
            if (!HandshakeRequest.TryParseBody(_msgEncoder, pVersion, out var request))
                return new RpcResult(RpcRetCode.InvalidHandshake, "TO DO");

            return RpcResult.Result(request);
        }

        public async Task<RpcResult<HandshakeResponse>> TryReceiveResponse(ByteTransport transport, CancellationToken timeoutToken)
        {
            _msgEncoder.Reset();

            // header 

            var headerRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, HandshakeRequest.HeaderSize, timeoutToken).ConfigureAwait(false);

            if (!headerRxResult.IsOk)
                return headerRxResult;

            _msgEncoder.Reset(HandshakeRequest.HeaderSize);
            var headerParseResult = HandshakeResponse.TryParseHeader(_msgEncoder, out var pVersion, out var size);

            if (headerParseResult != HandshakeParseResult.Ok)
                return new RpcResult<HandshakeResponse>(RpcRetCode.InvalidHandshake, "TO DO");

            // body

            var bodyRxResult = await transport.TryReceiveExact(_msgEncoder.Buffer, size, timeoutToken).ConfigureAwait(false);

            if (!bodyRxResult.IsOk)
                return bodyRxResult;

            _msgEncoder.Reset(size);
            if (!HandshakeResponse.TryParseBody(_msgEncoder, pVersion, out var request))
                return new RpcResult(RpcRetCode.InvalidHandshake, "TO DO");

            return RpcResult.Result(request);
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
        UnknownHostName = 2,
        UnknwonService = 3,
        OtherIncompatibility = 100
    }

    public struct ShortVersion
    {
        public ShortVersion(byte major, byte minor)
        {
            Major = major;
            Minor = minor;
        }

        public byte Major { get; set; }
        public byte Minor { get; set; }

        public static bool operator >=(ShortVersion a, ShortVersion b)
        {
            if (a.Major == b.Major)
                return a.Minor >= b.Minor;
            else
                return a.Major > b.Major;
        }

        public static bool operator <=(ShortVersion a, ShortVersion b)
        {
            if (a.Major == b.Major)
                return a.Minor <= b.Minor;
            else
                return a.Major < b.Major;
        }
    }

    internal struct HandshakeResult
    {
        public HandshakeResult(ServiceBinding sConfig, RpcVersionSpec rpcVersion)
        {
            Service = sConfig;
            VersionSpec = rpcVersion;
            WasAccepted = true;
        }

        public ServiceBinding Service { get; }
        public RpcVersionSpec VersionSpec { get; }
        public bool WasAccepted { get; }
    }
}
