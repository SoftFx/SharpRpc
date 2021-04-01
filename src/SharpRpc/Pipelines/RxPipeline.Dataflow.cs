using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpRpc
{
    partial class RxPipeline
    {
        internal class Dataflow : RxPipeline
        {
            private readonly ActionBlock<IRxTask> _parseBlock;
            private readonly CancellationTokenSource _stopSrc = new CancellationTokenSource();
            private DateTime _lastRxTime = DateTime.MaxValue;
            private TimeSpan _rxTimeThreshold;
            private Timer _healthCheckTimer;

            public Dataflow(ByteTransport transport, Endpoint config, IRpcSerializer serializer, MessageDispatcher messageConsumer)
                : base(transport, serializer, messageConsumer)
            {
                var parseBlockOptions = new ExecutionDataflowBlockOptions();
                parseBlockOptions.BoundedCapacity = 5;
                parseBlockOptions.MaxDegreeOfParallelism = 1;
                parseBlockOptions.CancellationToken = _stopSrc.Token;

                _rxTimeThreshold = config.RxTimeout;

                _parseBlock = new ActionBlock<IRxTask>(DoTask, parseBlockOptions);
            }

            public override void Start()
            {
                StartTransportRx();

                _healthCheckTimer = new Timer(OnHealthTimerTick, null, 1000, 1000);
            }

            public override async Task Close()
            {
                try
                {
                    _healthCheckTimer.Dispose();

                    _stopSrc.Cancel();
                    _parseBlock.Complete();
                    await _parseBlock.Completion;

                    await WaitStopTransportRx();
                }
                catch (TaskCanceledException)
                {
                }
            }

            private void DoTask(IRxTask task)
            {
                if (task is ParseTask pTask)
                {
                    _lastRxTime = DateTime.UtcNow;

                    if (_msgConsumer.SuportsBatching)
                        BatchParse(pTask);
                    else
                        Parse(pTask);
                }
                else if (task is DisconnectCheckTask)
                {
                    if (DateTime.UtcNow - _lastRxTime > _rxTimeThreshold)
                        SignalCommunicationError(new RpcResult(RpcRetCode.ConnectionTimeout, "Disconnected due to timeout."));
                }
            }

            private void OnHealthTimerTick(object state)
            {
                _parseBlock.Post(DisconnectCheckTask.Instance);
            }

            protected override ValueTask<bool> OnBytesArrived(int count)
            {
                var task = new ParseTask();
                task.BytesToConsume = _buffer.Advance(count, task);
                return new ValueTask<bool>(_parseBlock.SendAsync(task));
            }

            private interface IRxTask
            {
            }

            public class DisconnectCheckTask : IRxTask
            {
                public static DisconnectCheckTask Instance = new DisconnectCheckTask();
            }

            public class ParseTask : List<ArraySegment<byte>>, IRxTask
            {
                private int _bytesToConsume;

                public int BytesToConsume { get; set; }

                public new void Add(ArraySegment<byte> segment)
                {
                    _bytesToConsume += segment.Count;
                    base.Add(segment);
                }

                public void AdvanceConsume(int size, out bool allConsumed)
                {
                    BytesToConsume -= size;
                    allConsumed = BytesToConsume == 0;
                }
            }
        }
    }
}
