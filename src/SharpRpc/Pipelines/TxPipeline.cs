using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class TxPipeline
    {
        //private readonly ByteTransport _transport;
        private readonly Func<Task<RpcResult<ByteTransport>>> _transportRequestFunc;
        private Task _txLoop;

        public TxPipeline(Func<Task<RpcResult<ByteTransport>>> connectRequestFunc)
        {
            //_transport = transport;
            _transportRequestFunc = connectRequestFunc;
        }

        public ByteTransport Transport { get; protected set; }

        public event Action<RpcResult> CommunicationFaulted;

        public abstract RpcResult TrySend(IMessage message);
        public abstract void Send(IMessage message);
        public abstract ValueTask<RpcResult> TrySendAsync(IMessage message);
        public abstract ValueTask SendAsync(IMessage message);
        public abstract Task Close(RpcResult fault);

        protected abstract ValueTask ReturnSegmentAndDequeue(List<ArraySegment<byte>> container);

        protected Task<RpcResult<ByteTransport>> GetTransport()
        {
            return _transportRequestFunc();
        }

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted?.Invoke(fault);
        }

        protected void StartTransportRead()
        {
            _txLoop = TxBytesLoop();
        }

        protected Task WaitTransportRead()
        {
            return _txLoop;
        }

        private async Task TxBytesLoop()
        {
            try
            {
                var segmentList = new List<ArraySegment<byte>>();

                while (true)
                {
                    await ReturnSegmentAndDequeue(segmentList);
                    await Task.Yield();

                    try
                    {
                        var sentBytes = await Transport.Send(segmentList, CancellationToken.None);

                        if (sentBytes == 0)
                        {
                            SignalCommunicationError(new RpcResult(RpcRetCode.ConnectionShutdown, ""));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        var fault = Transport.TranslateException(ex);
                        SignalCommunicationError(fault);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                SignalCommunicationError(new RpcResult(RpcRetCode.OtherError, ex.Message));
            }
        }
    }
}
