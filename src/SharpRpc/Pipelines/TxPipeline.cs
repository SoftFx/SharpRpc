using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    internal abstract partial class TxPipeline
    {
        private Task _txLoop;
        private readonly CancellationTokenSource _txCancelSrc = new CancellationTokenSource();

        public ByteTransport Transport { get; protected set; }

        public event Action ConnectionRequested;
        public event Action<RpcResult> CommunicationFaulted;

        public abstract RpcResult TrySend(IMessage message);
        public abstract void Send(IMessage message);
        public abstract ValueTask<RpcResult> TrySendAsync(IMessage message);
        public abstract ValueTask<RpcResult> SendSystemMessage(ISystemMessage message);
        public abstract ValueTask SendAsync(IMessage message);
        public abstract void Start(ByteTransport transport);
        public abstract void StartProcessingUserMessages();
        public abstract void StopProcessingUserMessages(RpcResult fault);
        public abstract Task Close(TimeSpan gracefulCloseTimeout);

        protected abstract ValueTask<ArraySegment<byte>> DequeueNextSegment();

        protected void SignalCommunicationError(RpcResult fault)
        {
            CommunicationFaulted.Invoke(fault);
        }

        protected void StartTransportWrite()
        {
            _txLoop = TxBytesLoop();
        }

        protected void AbortTransportWriteAfter(TimeSpan timeSpan)
        {
            _txCancelSrc.CancelAfter(timeSpan);
        }

        protected void AbortTransportRead()
        {
            _txCancelSrc.Cancel();
        }

        protected Task WaitTransportWaitToEnd()
        {
            return _txLoop ?? Task.CompletedTask;
        }

        private async Task TxBytesLoop()
        {
            try
            {
                while (true)
                {
                    var data = await DequeueNextSegment();

                    if (data.Array == null)
                        return;

                    await Task.Yield();

                    try
                    {
                        await Transport.Send(data, _txCancelSrc.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // normal exit
                        return;
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

        protected void SignalConnectionRequest()
        {
            ConnectionRequested.Invoke();
        }
    }
}
