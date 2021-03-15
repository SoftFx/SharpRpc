using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class TcpTransport : ByteTransport
    {
        private readonly Socket _socket;

        public TcpTransport(Socket socket)
        {
            _socket = socket;
        }

        public override Task<int> Receive(IList<ArraySegment<byte>> buffers)
        {
            return _socket.ReceiveAsync(buffers, SocketFlags.None);

            //return Task.Factory.FromAsync((c, s) => _socket.BeginReceive(buffers, SocketFlags.None, c, s),
            //    (r) => _socket.EndReceive(r), null);
        }

        public override Task<int> Send(IList<ArraySegment<byte>> data, CancellationToken cancelToken)
        {
            return _socket.SendAsync(data, SocketFlags.None);
        }

        public override RpcResult TranslateException(Exception ex)
        {
            return ToRpcResult(ex);
        }

        public static RpcResult ToRpcResult(Exception ex)
        {
            if (ex is SocketException socketEx)
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

        public override Task Shutdown()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _socket.Dispose();
        }
    }
}
