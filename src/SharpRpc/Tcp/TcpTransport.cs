using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class TcpTransport : ByteTransport
    {
        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        
        public TcpTransport(Socket socket)
        {
            _socket = socket;
            _socket.NoDelay = true;
            _stream = new NetworkStream(socket, false);
        }

        public override ValueTask<int> Receive(ArraySegment<byte> buffer, CancellationToken cToken)
        {
            return _stream.ReadAsync(buffer, cToken);
        }

        public override ValueTask Send(ArraySegment<byte> data, CancellationToken cToken)
        {
            return _stream.WriteAsync(data, cToken);
        }

        public override RpcResult TranslateException(Exception ex)
        {
            return ToRpcResult(ex);
        }

        public static RpcResult ToRpcResult(Exception ex)
        {
            var socketEx = ex as SocketException ?? ex.InnerException as SocketException;

            if (socketEx != null)
            {
                switch (socketEx.SocketErrorCode)
                {
                    case SocketError.TimedOut: return new RpcResult(RpcRetCode.ConnectionTimeout, socketEx.Message);
                    case SocketError.Shutdown: return new RpcResult(RpcRetCode.ConnectionShutdown, socketEx.Message);
                    case SocketError.OperationAborted: return new RpcResult(RpcRetCode.ConnectionShutdown, socketEx.Message);
                    case SocketError.ConnectionAborted: return new RpcResult(RpcRetCode.ConnectionShutdown, ex.Message);
                    case SocketError.ConnectionReset: return new RpcResult(RpcRetCode.ConnectionAbortedByPeer, ex.Message);
                    default: return new RpcResult(RpcRetCode.OtherConnectionError, ex.Message);
                }
            }

            return new RpcResult(RpcRetCode.OtherConnectionError, "An unexpected exception is occurred in TcpTransport: " + ex.Message);
        }

        public override async Task Shutdown()
        {
            try
            {

                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
                // TO DO : log
            }

            try
            {
                await Task.Factory.FromAsync((c, s) => _socket.BeginDisconnect(false, c, s),
                    r => _socket.EndDisconnect(r), null);
            }
            catch (Exception)
            {
                // TO DO : log
            }

            _socket.Close();
        }

        public override void Dispose()
        {
            _stream.Dispose();
            _socket.Dispose();
        }
    }
}
