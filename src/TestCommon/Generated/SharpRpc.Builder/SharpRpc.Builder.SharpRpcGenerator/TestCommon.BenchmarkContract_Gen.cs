namespace TestCommon
{
    public class BenchmarkContract_Gen
    {
        public static Client CreateClient(SharpRpc.ClientEndpoint endpoint, CallbackServiceBase callbackHandler, SharpRpc.SerializerChoice serializer = SharpRpc.SerializerChoice.MessagePack)
        {
            var adapter = CreateSerializationAdapter(serializer);
            var descriptor = CreateDescriptor(adapter);
            return new Client(endpoint, descriptor, callbackHandler);
        }

        public static SharpRpc.ServiceBinding CreateBinding(System.Func<ServiceBase> serviceImplFactory, SharpRpc.SerializerChoice serializer = SharpRpc.SerializerChoice.MessagePack)
        {
            var adapter = CreateSerializationAdapter(serializer);
            var sFactory = new Messages();
            return new SharpRpc.ServiceBinding(() => new ServiceHandler(serviceImplFactory()), adapter, sFactory);
        }

        private static SharpRpc.IRpcSerializer CreateSerializationAdapter(SharpRpc.SerializerChoice serializer)
        {
            if (serializer == SharpRpc.SerializerChoice.MessagePack)
                return new MessagePackAdapter();
            else
                throw new SharpRpc.RpcConfigurationException("Specified serializer is not supported by this contract.");
        }

        private static SharpRpc.ContractDescriptor CreateDescriptor(SharpRpc.IRpcSerializer sAdapter)
        {
            return new SharpRpc.ContractDescriptor(sAdapter, new Messages());
        }

        public class Client : SharpRpc.ClientBase
        {
            public Client(SharpRpc.ClientEndpoint endpoint, SharpRpc.ContractDescriptor descriptor, CallbackServiceBase callbackHandler) : base(endpoint, descriptor, new CallbackServiceHandler(callbackHandler))
            {
                Async = new AsyncFacade(Channel);
                Try = new TryFacade(Channel);
                TryAsync = new TryAsyncFacade(Channel);
            }

            public AsyncFacade Async { get; }

            public TryFacade Try { get; }

            public TryAsyncFacade TryAsync { get; }

            public void SendUpdate(TestCommon.FooEntity entity)
            {
                Messages.C0_Message message = new Messages.C0_Message();
                message.Arg1 = entity;
                SendMessage(message);
            }

            public void SendUpdate(PrebuiltMessages.SendUpdate message)
            {
                SendMessage(message);
            }

            public void ApplyUpdate(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C1_Request message = new Messages.C1_Request();
                message.Arg1 = entity;
                CallAsync<Messages.C1_Response>(message, cancelToken).Wait();
            }

            public SharpRpc.InputStreamCall<TestCommon.FooEntity> UpstreamUpdates(SharpRpc.StreamOptions streamOptions)
            {
                Messages.C2_Request message = new Messages.C2_Request();
                return OpenInputStream<TestCommon.FooEntity>(message, streamOptions, new Messages.C2_InputStreamFactory());
            }

            public SharpRpc.OutputStreamCall<TestCommon.FooEntity> DownstreamUpdates(SharpRpc.StreamOptions streamOptions)
            {
                Messages.C3_Request message = new Messages.C3_Request();
                return OpenOutputStream<TestCommon.FooEntity>(message, streamOptions, new Messages.C3_OutputStreamFactory());
            }

            public void Flush(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C4_Request message = new Messages.C4_Request();
                CallAsync<Messages.C4_Response>(message, cancelToken).Wait();
            }

            public TestCommon.MulticastReport MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C5_Request message = new Messages.C5_Request();
                message.Arg1 = msgCount;
                message.Arg2 = usePrebuiltMessages;
                return CallAsync<TestCommon.MulticastReport, Messages.C5_Response>(message, cancelToken).Result;
            }

            public TestCommon.PerfReport GetPerfCounters(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C8_Request message = new Messages.C8_Request();
                return CallAsync<TestCommon.PerfReport, Messages.C8_Response>(message, cancelToken).Result;
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task SendUpdate(TestCommon.FooEntity entity)
                {
                    Messages.C0_Message message = new Messages.C0_Message();
                    message.Arg1 = entity;
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task SendUpdate(PrebuiltMessages.SendUpdate message)
                {
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task ApplyUpdate(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = entity;
                    return CallAsync<Messages.C1_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task Flush(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C4_Request message = new Messages.C4_Request();
                    return CallAsync<Messages.C4_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<TestCommon.MulticastReport> MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C5_Request message = new Messages.C5_Request();
                    message.Arg1 = msgCount;
                    message.Arg2 = usePrebuiltMessages;
                    return CallAsync<TestCommon.MulticastReport, Messages.C5_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<TestCommon.PerfReport> GetPerfCounters(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C8_Request message = new Messages.C8_Request();
                    return CallAsync<TestCommon.PerfReport, Messages.C8_Response>(message, cancelToken);
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public SharpRpc.RpcResult SendUpdate(TestCommon.FooEntity entity)
                {
                    Messages.C0_Message message = new Messages.C0_Message();
                    message.Arg1 = entity;
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult SendUpdate(PrebuiltMessages.SendUpdate message)
                {
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult ApplyUpdate(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = entity;
                    return TryCallAsync<Messages.C1_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult Flush(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C4_Request message = new Messages.C4_Request();
                    return TryCallAsync<Messages.C4_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<TestCommon.MulticastReport> MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C5_Request message = new Messages.C5_Request();
                    message.Arg1 = msgCount;
                    message.Arg2 = usePrebuiltMessages;
                    return TryCallAsync<TestCommon.MulticastReport, Messages.C5_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<TestCommon.PerfReport> GetPerfCounters(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C8_Request message = new Messages.C8_Request();
                    return TryCallAsync<TestCommon.PerfReport, Messages.C8_Response>(message, cancelToken).Result;
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> SendUpdate(TestCommon.FooEntity entity)
                {
                    Messages.C0_Message message = new Messages.C0_Message();
                    message.Arg1 = entity;
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> SendUpdate(PrebuiltMessages.SendUpdate message)
                {
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> ApplyUpdate(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = entity;
                    return TryCallAsync<Messages.C1_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> Flush(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C4_Request message = new Messages.C4_Request();
                    return TryCallAsync<Messages.C4_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<TestCommon.MulticastReport>> MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C5_Request message = new Messages.C5_Request();
                    message.Arg1 = msgCount;
                    message.Arg2 = usePrebuiltMessages;
                    return TryCallAsync<TestCommon.MulticastReport, Messages.C5_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<TestCommon.PerfReport>> GetPerfCounters(System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C8_Request message = new Messages.C8_Request();
                    return TryCallAsync<TestCommon.PerfReport, Messages.C8_Response>(message, cancelToken);
                }
            }
        }

        public abstract class ServiceBase
        {
            public abstract System.Threading.Tasks.Task SendUpdate(TestCommon.FooEntity entity);
            public abstract System.Threading.Tasks.Task ApplyUpdate(SharpRpc.CallContext context, TestCommon.FooEntity entity);
            public abstract System.Threading.Tasks.Task UpstreamUpdates(SharpRpc.CallContext context, SharpRpc.StreamReader<TestCommon.FooEntity> inputStream);
            public abstract System.Threading.Tasks.Task DownstreamUpdates(SharpRpc.CallContext context, SharpRpc.StreamWriter<TestCommon.FooEntity> outputStream);
            public abstract System.Threading.Tasks.Task Flush(SharpRpc.CallContext context);
            public abstract System.Threading.Tasks.Task<TestCommon.MulticastReport> MulticastUpdateToClients(SharpRpc.CallContext context, int msgCount, bool usePrebuiltMessages);
            public abstract System.Threading.Tasks.Task<TestCommon.PerfReport> GetPerfCounters(SharpRpc.CallContext context);
            public SharpRpc.SessionInfo Session { get; private set; }

            public CallbackClient Client { get; private set; }

            public virtual void OnInit()
            {
            }

            public virtual void OnClose()
            {
            }

            public void InitServiceStub(SharpRpc.SessionInfo session, CallbackClient client)
            {
                Session = session;
                Client = client;
                OnInit();
            }
        }

        private class ServiceHandler : SharpRpc.ServiceCallHandler
        {
            ServiceBase _stub;
            public ServiceHandler(ServiceBase serviceImpl)
            {
                _stub = serviceImpl;
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeApplyUpdate(Messages.C1_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.ApplyUpdate(context, request.Arg1);
                    CloseContext(context);
                    var response = new Messages.C1_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C1_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C1_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeUpstreamUpdates(Messages.C2_Request request)
            {
                var context = CreateInputStreamContext<TestCommon.FooEntity>(request, new Messages.C2_InputStreamFactory());
                try
                {
                    await _stub.UpstreamUpdates(context, context.InputStream);
                    await CloseStreamContext(context);
                    var response = new Messages.C2_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C2_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C2_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeDownstreamUpdates(Messages.C3_Request request)
            {
                var context = CreateOutputStreamContext<TestCommon.FooEntity>(request, new Messages.C3_OutputStreamFactory());
                try
                {
                    await _stub.DownstreamUpdates(context, context.OutputStream);
                    await CloseStreamContext(context);
                    var response = new Messages.C3_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C3_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C3_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeFlush(Messages.C4_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.Flush(context);
                    CloseContext(context);
                    var response = new Messages.C4_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C4_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C4_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeMulticastUpdateToClients(Messages.C5_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.MulticastUpdateToClients(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C5_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C5_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C5_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeGetPerfCounters(Messages.C8_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.GetPerfCounters(context);
                    CloseContext(context);
                    var response = new Messages.C8_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C8_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C8_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            protected override System.Threading.Tasks.Task OnMessage(SharpRpc.IMessage message)
            {
                if (message is Messages.C0_Message)
                {
                    var m0 = (Messages.C0_Message)message;
                    return _stub.SendUpdate(m0.Arg1);
                }
                else
                    return OnUnknownMessage(message);
            }

            protected override System.Threading.Tasks.Task<SharpRpc.IResponseMessage> OnRequest(SharpRpc.IRequestMessage request)
            {
                if (request is Messages.C8_Request)
                {
                    var r5 = (Messages.C8_Request)request;
                    return InvokeGetPerfCounters(r5);
                }
                else if (request is Messages.C5_Request)
                {
                    var r4 = (Messages.C5_Request)request;
                    return InvokeMulticastUpdateToClients(r4);
                }
                else if (request is Messages.C4_Request)
                {
                    var r3 = (Messages.C4_Request)request;
                    return InvokeFlush(r3);
                }
                else if (request is Messages.C3_Request)
                {
                    var r2 = (Messages.C3_Request)request;
                    return InvokeDownstreamUpdates(r2);
                }
                else if (request is Messages.C2_Request)
                {
                    var r1 = (Messages.C2_Request)request;
                    return InvokeUpstreamUpdates(r1);
                }
                else if (request is Messages.C1_Request)
                {
                    var r0 = (Messages.C1_Request)request;
                    return InvokeApplyUpdate(r0);
                }
                else
                    return OnUnknownRequest(request);
            }

            protected override void OnInit(SharpRpc.Channel channel)
            {
                _stub.InitServiceStub(Session, new CallbackClient(channel));
            }

            protected override void OnClose()
            {
                _stub.OnClose();
            }
        }

        private class MessagePackAdapter : SharpRpc.IRpcSerializer
        {
            public void Serialize(SharpRpc.IMessage message, SharpRpc.MessageWriter writer)
            {
                MessagePack.MessagePackSerializer.Serialize<TestCommon.BenchmarkContract_Gen.Messages.MessageBase>(writer.ByteStream, (TestCommon.BenchmarkContract_Gen.Messages.MessageBase)message);
            }

            public SharpRpc.IMessage Deserialize(SharpRpc.MessageReader reader)
            {
                return MessagePack.MessagePackSerializer.Deserialize<TestCommon.BenchmarkContract_Gen.Messages.MessageBase>(reader.ByteStream);
            }
        }

        public class Messages : SharpRpc.IMessageFactory
        {
            public SharpRpc.ILoginMessage CreateLoginMessage()
            {
                return new Messages.Login();
            }

            public SharpRpc.ILogoutMessage CreateLogoutMessage()
            {
                return new Messages.Logout();
            }

            public SharpRpc.IHeartbeatMessage CreateHeartBeatMessage()
            {
                return new Messages.Heartbeat();
            }

            public SharpRpc.ICancelRequestMessage CreateCancelRequestMessage()
            {
                return new Messages.CancelRequest();
            }

            [MessagePack.UnionAttribute(1, typeof(Messages.Login))]
            [MessagePack.UnionAttribute(2, typeof(Messages.Logout))]
            [MessagePack.UnionAttribute(3, typeof(Messages.Heartbeat))]
            [MessagePack.UnionAttribute(7, typeof(Messages.PageAcknowledgement))]
            [MessagePack.UnionAttribute(9, typeof(Messages.CancelStream))]
            [MessagePack.UnionAttribute(8, typeof(Messages.CloseStream))]
            [MessagePack.UnionAttribute(10, typeof(Messages.CloseStreamAck))]
            [MessagePack.UnionAttribute(4, typeof(Messages.CancelRequest))]
            [MessagePack.UnionAttribute(30, typeof(Messages.C0_Message))]
            [MessagePack.UnionAttribute(35, typeof(Messages.C1_Request))]
            [MessagePack.UnionAttribute(36, typeof(Messages.C1_Response))]
            [MessagePack.UnionAttribute(37, typeof(Messages.C1_Fault))]
            [MessagePack.UnionAttribute(40, typeof(Messages.C2_Request))]
            [MessagePack.UnionAttribute(41, typeof(Messages.C2_Response))]
            [MessagePack.UnionAttribute(42, typeof(Messages.C2_Fault))]
            [MessagePack.UnionAttribute(43, typeof(Messages.C2_InputPage))]
            [MessagePack.UnionAttribute(45, typeof(Messages.C3_Request))]
            [MessagePack.UnionAttribute(46, typeof(Messages.C3_Response))]
            [MessagePack.UnionAttribute(47, typeof(Messages.C3_Fault))]
            [MessagePack.UnionAttribute(49, typeof(Messages.C3_OutputPage))]
            [MessagePack.UnionAttribute(50, typeof(Messages.C4_Request))]
            [MessagePack.UnionAttribute(51, typeof(Messages.C4_Response))]
            [MessagePack.UnionAttribute(52, typeof(Messages.C4_Fault))]
            [MessagePack.UnionAttribute(55, typeof(Messages.C5_Request))]
            [MessagePack.UnionAttribute(56, typeof(Messages.C5_Response))]
            [MessagePack.UnionAttribute(57, typeof(Messages.C5_Fault))]
            [MessagePack.UnionAttribute(60, typeof(Messages.C6_Message))]
            [MessagePack.UnionAttribute(65, typeof(Messages.C7_Request))]
            [MessagePack.UnionAttribute(66, typeof(Messages.C7_Response))]
            [MessagePack.UnionAttribute(67, typeof(Messages.C7_Fault))]
            [MessagePack.UnionAttribute(70, typeof(Messages.C8_Request))]
            [MessagePack.UnionAttribute(71, typeof(Messages.C8_Response))]
            [MessagePack.UnionAttribute(72, typeof(Messages.C8_Fault))]
            public interface MessageBase : global::SharpRpc.IMessage
            {
            }

            public class C2_InputStreamFactory : SharpRpc.IStreamMessageFactory<TestCommon.FooEntity>
            {
                public SharpRpc.IStreamPage<TestCommon.FooEntity> CreatePage(string streamId)
                {
                    return new C2_InputPage(streamId);
                }

                public SharpRpc.IStreamCloseMessage CreateCloseMessage(string streamId)
                {
                    return new CloseStream(streamId);
                }

                public SharpRpc.IStreamCloseAckMessage CreateCloseAcknowledgement(string streamId)
                {
                    return new CloseStreamAck(streamId);
                }

                public SharpRpc.IStreamCancelMessage CreateCancelMessage(string streamId)
                {
                    return new CancelStream(streamId);
                }

                public SharpRpc.IStreamPageAck CreatePageAcknowledgement(string streamId)
                {
                    return new PageAcknowledgement(streamId);
                }
            }

            public class C3_OutputStreamFactory : SharpRpc.IStreamMessageFactory<TestCommon.FooEntity>
            {
                public SharpRpc.IStreamPage<TestCommon.FooEntity> CreatePage(string streamId)
                {
                    return new C3_OutputPage(streamId);
                }

                public SharpRpc.IStreamCloseMessage CreateCloseMessage(string streamId)
                {
                    return new CloseStream(streamId);
                }

                public SharpRpc.IStreamCloseAckMessage CreateCloseAcknowledgement(string streamId)
                {
                    return new CloseStreamAck(streamId);
                }

                public SharpRpc.IStreamCancelMessage CreateCancelMessage(string streamId)
                {
                    return new CancelStream(streamId);
                }

                public SharpRpc.IStreamPageAck CreatePageAcknowledgement(string streamId)
                {
                    return new PageAcknowledgement(streamId);
                }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class Login : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.ILoginMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string UserName { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Password { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.LoginResult? ResultCode { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string ErrorMessage { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class Logout : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.ILogoutMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class Heartbeat : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IHeartbeatMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class PageAcknowledgement : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IStreamPageAck
            {
                public PageAcknowledgement(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public ushort Consumed { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CancelStream : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IStreamCancelMessage
            {
                public CancelStream(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.StreamCancelOptions Options { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CloseStream : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseMessage
            {
                public CloseStream(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.StreamCloseOptions Options { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CloseStreamAck : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseAckMessage
            {
                public CloseStreamAck(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CancelRequest : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.ICancelRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C0_Message : MessageBase
            {
                [MessagePack.KeyAttribute(0)]
                public TestCommon.FooEntity Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public TestCommon.FooEntity Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C2_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public ushort? WindowSize { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C2_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C2_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C2_InputPage : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<TestCommon.FooEntity>
            {
                public C2_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<TestCommon.FooEntity> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C3_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public ushort? WindowSize { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C3_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C3_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C3_OutputPage : TestCommon.BenchmarkContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<TestCommon.FooEntity>
            {
                public C3_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<TestCommon.FooEntity> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C4_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C4_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C4_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public bool Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Response : MessageBase, SharpRpc.IResponseMessage<TestCommon.MulticastReport>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public TestCommon.MulticastReport Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_Message : MessageBase
            {
                [MessagePack.KeyAttribute(0)]
                public TestCommon.FooEntity Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C7_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public TestCommon.FooEntity Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C7_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C7_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C8_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C8_Response : MessageBase, SharpRpc.IResponseMessage<TestCommon.PerfReport>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public TestCommon.PerfReport Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C8_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return null;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }
            }
        }

        public class CallbackClient : SharpRpc.ClientBase
        {
            public CallbackClient(SharpRpc.Channel channel) : base(channel)
            {
                Async = new AsyncFacade(Channel);
                Try = new TryFacade(Channel);
                TryAsync = new TryAsyncFacade(Channel);
            }

            public AsyncFacade Async { get; }

            public TryFacade Try { get; }

            public TryAsyncFacade TryAsync { get; }

            public void SendUpdateToClient(TestCommon.FooEntity entity)
            {
                Messages.C6_Message message = new Messages.C6_Message();
                message.Arg1 = entity;
                SendMessage(message);
            }

            public void SendUpdateToClient(PrebuiltMessages.SendUpdateToClient message)
            {
                SendMessage(message);
            }

            public void ApplyUpdateOnClient(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C7_Request message = new Messages.C7_Request();
                message.Arg1 = entity;
                CallAsync<Messages.C7_Response>(message, cancelToken).Wait();
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task SendUpdateToClient(TestCommon.FooEntity entity)
                {
                    Messages.C6_Message message = new Messages.C6_Message();
                    message.Arg1 = entity;
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task SendUpdateToClient(PrebuiltMessages.SendUpdateToClient message)
                {
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task ApplyUpdateOnClient(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C7_Request message = new Messages.C7_Request();
                    message.Arg1 = entity;
                    return CallAsync<Messages.C7_Response>(message, cancelToken);
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public SharpRpc.RpcResult SendUpdateToClient(TestCommon.FooEntity entity)
                {
                    Messages.C6_Message message = new Messages.C6_Message();
                    message.Arg1 = entity;
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult SendUpdateToClient(PrebuiltMessages.SendUpdateToClient message)
                {
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult ApplyUpdateOnClient(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C7_Request message = new Messages.C7_Request();
                    message.Arg1 = entity;
                    return TryCallAsync<Messages.C7_Response>(message, cancelToken).Result;
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> SendUpdateToClient(TestCommon.FooEntity entity)
                {
                    Messages.C6_Message message = new Messages.C6_Message();
                    message.Arg1 = entity;
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> SendUpdateToClient(PrebuiltMessages.SendUpdateToClient message)
                {
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> ApplyUpdateOnClient(TestCommon.FooEntity entity, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C7_Request message = new Messages.C7_Request();
                    message.Arg1 = entity;
                    return TryCallAsync<Messages.C7_Response>(message, cancelToken);
                }
            }
        }

        public abstract class CallbackServiceBase
        {
            public abstract System.Threading.Tasks.Task SendUpdateToClient(TestCommon.FooEntity entity);
            public abstract System.Threading.Tasks.Task ApplyUpdateOnClient(SharpRpc.CallContext context, TestCommon.FooEntity entity);
        }

        private class CallbackServiceHandler : SharpRpc.RpcCallHandler
        {
            CallbackServiceBase _stub;
            public CallbackServiceHandler(CallbackServiceBase serviceImpl)
            {
                _stub = serviceImpl;
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeApplyUpdateOnClient(Messages.C7_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.ApplyUpdateOnClient(context, request.Arg1);
                    CloseContext(context);
                    var response = new Messages.C7_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C7_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C7_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            protected override System.Threading.Tasks.Task OnMessage(SharpRpc.IMessage message)
            {
                if (message is Messages.C6_Message)
                {
                    var m0 = (Messages.C6_Message)message;
                    return _stub.SendUpdateToClient(m0.Arg1);
                }
                else
                    return OnUnknownMessage(message);
            }

            protected override System.Threading.Tasks.Task<SharpRpc.IResponseMessage> OnRequest(SharpRpc.IRequestMessage request)
            {
                if (request is Messages.C7_Request)
                {
                    var r0 = (Messages.C7_Request)request;
                    return InvokeApplyUpdateOnClient(r0);
                }
                else
                    return OnUnknownRequest(request);
            }
        }

        public class Prebuilder
        {
            private readonly SharpRpc.PreserializeTool _preserializer;
            public Prebuilder()
            {
                _preserializer = new SharpRpc.PreserializeTool(new MessagePackAdapter());
            }

            public PrebuiltMessages.SendUpdate PrebuildSendUpdate(TestCommon.FooEntity entity)
            {
                Messages.C0_Message message = new Messages.C0_Message();
                message.Arg1 = entity;
                var bytes = _preserializer.SerializeOnSingleAdapter(message);
                return new PrebuiltMessages.SendUpdate(bytes);
            }

            public PrebuiltMessages.SendUpdateToClient PrebuildSendUpdateToClient(TestCommon.FooEntity entity)
            {
                Messages.C6_Message message = new Messages.C6_Message();
                message.Arg1 = entity;
                var bytes = _preserializer.SerializeOnSingleAdapter(message);
                return new PrebuiltMessages.SendUpdateToClient(bytes);
            }
        }

        public class PrebuiltMessages
        {
            public class SendUpdate : SharpRpc.PrebuiltMessage
            {
                public SendUpdate(SharpRpc.SegmentedByteArray bytes) : base(bytes)
                {
                }
            }

            public class SendUpdateToClient : SharpRpc.PrebuiltMessage
            {
                public SendUpdateToClient(SharpRpc.SegmentedByteArray bytes) : base(bytes)
                {
                }
            }
        }
    }
}