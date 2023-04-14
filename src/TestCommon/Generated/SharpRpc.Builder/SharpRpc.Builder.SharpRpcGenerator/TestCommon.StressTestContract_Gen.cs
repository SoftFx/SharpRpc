namespace TestCommon
{
    public class StressTestContract_Gen
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

            public TestCommon.StressEntity RequestResponse(TestCommon.StressEntity entity, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C1_Request message = new Messages.C1_Request();
                message.Arg1 = entity;
                message.Arg2 = cfg;
                return CallAsync<TestCommon.StressEntity, Messages.C1_Response>(message, cancelToken).Result;
            }

            public void RequestMessages(int count, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C2_Request message = new Messages.C2_Request();
                message.Arg1 = count;
                message.Arg2 = cfg;
                CallAsync<Messages.C2_Response>(message, cancelToken).Wait();
            }

            public SharpRpc.OutputStreamCall<TestCommon.StressEntity> DownstreamEntities(SharpRpc.StreamOptions streamOptions, TestCommon.RequestConfig cfg, int count)
            {
                Messages.C4_Request message = new Messages.C4_Request();
                message.Arg1 = cfg;
                message.Arg2 = count;
                return OpenOutputStream<TestCommon.StressEntity>(message, streamOptions, new Messages.C4_OutputStreamFactory());
            }

            public SharpRpc.InputStreamCall<TestCommon.StressEntity, int> UpstreamEntities(SharpRpc.StreamOptions streamOptions, TestCommon.RequestConfig cfg)
            {
                Messages.C5_Request message = new Messages.C5_Request();
                message.Arg1 = cfg;
                return OpenInputStream<TestCommon.StressEntity, int>(message, streamOptions, new Messages.C5_InputStreamFactory());
            }

            public SharpRpc.DuplexStreamCall<TestCommon.StressEntity, TestCommon.StressEntity> DuplexStreamEntities(SharpRpc.DuplexStreamOptions streamOptions, TestCommon.RequestConfig cfg)
            {
                Messages.C6_Request message = new Messages.C6_Request();
                message.Arg1 = cfg;
                return OpenDuplexStream<TestCommon.StressEntity, TestCommon.StressEntity>(message, streamOptions, new Messages.C6_InputStreamFactory(), new Messages.C6_OutputStreamFactory());
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<TestCommon.StressEntity> RequestResponse(TestCommon.StressEntity entity, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = entity;
                    message.Arg2 = cfg;
                    return CallAsync<TestCommon.StressEntity, Messages.C1_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task RequestMessages(int count, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C2_Request message = new Messages.C2_Request();
                    message.Arg1 = count;
                    message.Arg2 = cfg;
                    return CallAsync<Messages.C2_Response>(message, cancelToken);
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public SharpRpc.RpcResult<TestCommon.StressEntity> RequestResponse(TestCommon.StressEntity entity, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = entity;
                    message.Arg2 = cfg;
                    return TryCallAsync<TestCommon.StressEntity, Messages.C1_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult RequestMessages(int count, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C2_Request message = new Messages.C2_Request();
                    message.Arg1 = count;
                    message.Arg2 = cfg;
                    return TryCallAsync<Messages.C2_Response>(message, cancelToken).Result;
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<TestCommon.StressEntity>> RequestResponse(TestCommon.StressEntity entity, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = entity;
                    message.Arg2 = cfg;
                    return TryCallAsync<TestCommon.StressEntity, Messages.C1_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> RequestMessages(int count, TestCommon.RequestConfig cfg, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C2_Request message = new Messages.C2_Request();
                    message.Arg1 = count;
                    message.Arg2 = cfg;
                    return TryCallAsync<Messages.C2_Response>(message, cancelToken);
                }
            }
        }

        public abstract class ServiceBase
        {
            public abstract System.Threading.Tasks.Task<TestCommon.StressEntity> RequestResponse(SharpRpc.CallContext context, TestCommon.StressEntity entity, TestCommon.RequestConfig cfg);
            public abstract System.Threading.Tasks.Task RequestMessages(SharpRpc.CallContext context, int count, TestCommon.RequestConfig cfg);
            public abstract System.Threading.Tasks.Task DownstreamEntities(SharpRpc.CallContext context, SharpRpc.StreamWriter<TestCommon.StressEntity> outputStream, TestCommon.RequestConfig cfg, int count);
            public abstract System.Threading.Tasks.Task<int> UpstreamEntities(SharpRpc.CallContext context, SharpRpc.StreamReader<TestCommon.StressEntity> inputStream, TestCommon.RequestConfig cfg);
            public abstract System.Threading.Tasks.Task DuplexStreamEntities(SharpRpc.CallContext context, SharpRpc.StreamReader<TestCommon.StressEntity> inputStream, SharpRpc.StreamWriter<TestCommon.StressEntity> outputStream, TestCommon.RequestConfig cfg);
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeRequestResponse(Messages.C1_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.RequestResponse(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C1_Response();
                    response.Result = result;
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeRequestMessages(Messages.C2_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.RequestMessages(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C2_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C2_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C2_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeDownstreamEntities(Messages.C4_Request request)
            {
                var context = CreateOutputStreamContext<TestCommon.StressEntity>(request, new Messages.C4_OutputStreamFactory());
                try
                {
                    await _stub.DownstreamEntities(context, context.OutputStream, request.Arg1, request.Arg2);
                    await CloseStreamContext(context);
                    var response = new Messages.C4_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C4_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C4_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeUpstreamEntities(Messages.C5_Request request)
            {
                var context = CreateInputStreamContext<TestCommon.StressEntity>(request, new Messages.C5_InputStreamFactory());
                try
                {
                    var result = await _stub.UpstreamEntities(context, context.InputStream, request.Arg1);
                    await CloseStreamContext(context);
                    var response = new Messages.C5_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C5_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C5_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeDuplexStreamEntities(Messages.C6_Request request)
            {
                var context = CreateDuplexStreamContext<TestCommon.StressEntity, TestCommon.StressEntity>(request, new Messages.C6_InputStreamFactory(), new Messages.C6_OutputStreamFactory());
                try
                {
                    await _stub.DuplexStreamEntities(context, context.InputStream, context.OutputStream, request.Arg1);
                    await CloseStreamContext(context);
                    var response = new Messages.C6_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C6_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C6_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            protected override System.Threading.Tasks.Task OnMessage(SharpRpc.IMessage message)
            {
                return OnUnknownMessage(message);
            }

            protected override System.Threading.Tasks.Task<SharpRpc.IResponseMessage> OnRequest(SharpRpc.IRequestMessage request)
            {
                if (request is Messages.C6_Request)
                {
                    var r4 = (Messages.C6_Request)request;
                    return InvokeDuplexStreamEntities(r4);
                }
                else if (request is Messages.C5_Request)
                {
                    var r3 = (Messages.C5_Request)request;
                    return InvokeUpstreamEntities(r3);
                }
                else if (request is Messages.C4_Request)
                {
                    var r2 = (Messages.C4_Request)request;
                    return InvokeDownstreamEntities(r2);
                }
                else if (request is Messages.C2_Request)
                {
                    var r1 = (Messages.C2_Request)request;
                    return InvokeRequestMessages(r1);
                }
                else if (request is Messages.C1_Request)
                {
                    var r0 = (Messages.C1_Request)request;
                    return InvokeRequestResponse(r0);
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
                MessagePack.MessagePackSerializer.Serialize<TestCommon.StressTestContract_Gen.Messages.MessageBase>(writer.ByteStream, (TestCommon.StressTestContract_Gen.Messages.MessageBase)message);
            }

            public SharpRpc.IMessage Deserialize(SharpRpc.MessageReader reader)
            {
                return MessagePack.MessagePackSerializer.Deserialize<TestCommon.StressTestContract_Gen.Messages.MessageBase>(reader.ByteStream);
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
            [MessagePack.UnionAttribute(35, typeof(Messages.C1_Request))]
            [MessagePack.UnionAttribute(36, typeof(Messages.C1_Response))]
            [MessagePack.UnionAttribute(37, typeof(Messages.C1_Fault))]
            [MessagePack.UnionAttribute(40, typeof(Messages.C2_Request))]
            [MessagePack.UnionAttribute(41, typeof(Messages.C2_Response))]
            [MessagePack.UnionAttribute(42, typeof(Messages.C2_Fault))]
            [MessagePack.UnionAttribute(45, typeof(Messages.C3_Message))]
            [MessagePack.UnionAttribute(50, typeof(Messages.C4_Request))]
            [MessagePack.UnionAttribute(51, typeof(Messages.C4_Response))]
            [MessagePack.UnionAttribute(52, typeof(Messages.C4_Fault))]
            [MessagePack.UnionAttribute(54, typeof(Messages.C4_OutputPage))]
            [MessagePack.UnionAttribute(55, typeof(Messages.C5_Request))]
            [MessagePack.UnionAttribute(56, typeof(Messages.C5_Response))]
            [MessagePack.UnionAttribute(57, typeof(Messages.C5_Fault))]
            [MessagePack.UnionAttribute(58, typeof(Messages.C5_InputPage))]
            [MessagePack.UnionAttribute(60, typeof(Messages.C6_Request))]
            [MessagePack.UnionAttribute(61, typeof(Messages.C6_Response))]
            [MessagePack.UnionAttribute(62, typeof(Messages.C6_Fault))]
            [MessagePack.UnionAttribute(63, typeof(Messages.C6_InputPage))]
            [MessagePack.UnionAttribute(64, typeof(Messages.C6_OutputPage))]
            public interface MessageBase : global::SharpRpc.IMessage
            {
            }

            public class C4_OutputStreamFactory : SharpRpc.IStreamMessageFactory<TestCommon.StressEntity>
            {
                public SharpRpc.IStreamPage<TestCommon.StressEntity> CreatePage(string streamId)
                {
                    return new C4_OutputPage(streamId);
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

            public class C5_InputStreamFactory : SharpRpc.IStreamMessageFactory<TestCommon.StressEntity>
            {
                public SharpRpc.IStreamPage<TestCommon.StressEntity> CreatePage(string streamId)
                {
                    return new C5_InputPage(streamId);
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

            public class C6_InputStreamFactory : SharpRpc.IStreamMessageFactory<TestCommon.StressEntity>
            {
                public SharpRpc.IStreamPage<TestCommon.StressEntity> CreatePage(string streamId)
                {
                    return new C6_InputPage(streamId);
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

            public class C6_OutputStreamFactory : SharpRpc.IStreamMessageFactory<TestCommon.StressEntity>
            {
                public SharpRpc.IStreamPage<TestCommon.StressEntity> CreatePage(string streamId)
                {
                    return new C6_OutputPage(streamId);
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
            public class Login : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.ILoginMessage
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
            public class Logout : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.ILogoutMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class Heartbeat : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IHeartbeatMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class PageAcknowledgement : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPageAck
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
            public class CancelStream : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCancelMessage
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
            public class CloseStream : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseMessage
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
            public class CloseStreamAck : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseAckMessage
            {
                public CloseStreamAck(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CancelRequest : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.ICancelRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public TestCommon.StressEntity Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public TestCommon.RequestConfig Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Response : MessageBase, SharpRpc.IResponseMessage<TestCommon.StressEntity>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public TestCommon.StressEntity Result { get; set; }
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
            public class C2_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public TestCommon.RequestConfig Arg2 { get; set; }
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
            public class C3_Message : MessageBase
            {
                [MessagePack.KeyAttribute(0)]
                public System.Guid Arg1 { get; set; }

                [MessagePack.KeyAttribute(1)]
                public TestCommon.StressEntity Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C4_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public ushort? WindowSize { get; set; }

                [MessagePack.KeyAttribute(3)]
                public TestCommon.RequestConfig Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public int Arg2 { get; set; }
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
            public class C4_OutputPage : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<TestCommon.StressEntity>
            {
                public C4_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<TestCommon.StressEntity> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public ushort? WindowSize { get; set; }

                [MessagePack.KeyAttribute(3)]
                public TestCommon.RequestConfig Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Response : MessageBase, SharpRpc.IResponseMessage<int>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public int Result { get; set; }
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
            public class C5_InputPage : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<TestCommon.StressEntity>
            {
                public C5_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<TestCommon.StressEntity> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public ushort? WindowSize { get; set; }

                [MessagePack.KeyAttribute(3)]
                public TestCommon.RequestConfig Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C6_InputPage : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<TestCommon.StressEntity>
            {
                public C6_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<TestCommon.StressEntity> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_OutputPage : TestCommon.StressTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<TestCommon.StressEntity>
            {
                public C6_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<TestCommon.StressEntity> Items { get; set; }
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

            public void CallbackMessage(System.Guid requestId, TestCommon.StressEntity entity)
            {
                Messages.C3_Message message = new Messages.C3_Message();
                message.Arg1 = requestId;
                message.Arg2 = entity;
                SendMessage(message);
            }

            public void CallbackMessage(PrebuiltMessages.CallbackMessage message)
            {
                SendMessage(message);
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task CallbackMessage(System.Guid requestId, TestCommon.StressEntity entity)
                {
                    Messages.C3_Message message = new Messages.C3_Message();
                    message.Arg1 = requestId;
                    message.Arg2 = entity;
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task CallbackMessage(PrebuiltMessages.CallbackMessage message)
                {
                    return SendMessageAsync(message);
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public SharpRpc.RpcResult CallbackMessage(System.Guid requestId, TestCommon.StressEntity entity)
                {
                    Messages.C3_Message message = new Messages.C3_Message();
                    message.Arg1 = requestId;
                    message.Arg2 = entity;
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult CallbackMessage(PrebuiltMessages.CallbackMessage message)
                {
                    return TrySendMessage(message);
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> CallbackMessage(System.Guid requestId, TestCommon.StressEntity entity)
                {
                    Messages.C3_Message message = new Messages.C3_Message();
                    message.Arg1 = requestId;
                    message.Arg2 = entity;
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> CallbackMessage(PrebuiltMessages.CallbackMessage message)
                {
                    return TrySendMessageAsync(message);
                }
            }
        }

        public abstract class CallbackServiceBase
        {
            public abstract System.Threading.Tasks.Task CallbackMessage(System.Guid requestId, TestCommon.StressEntity entity);
        }

        private class CallbackServiceHandler : SharpRpc.RpcCallHandler
        {
            CallbackServiceBase _stub;
            public CallbackServiceHandler(CallbackServiceBase serviceImpl)
            {
                _stub = serviceImpl;
            }

            protected override System.Threading.Tasks.Task OnMessage(SharpRpc.IMessage message)
            {
                if (message is Messages.C3_Message)
                {
                    var m0 = (Messages.C3_Message)message;
                    return _stub.CallbackMessage(m0.Arg1, m0.Arg2);
                }
                else
                    return OnUnknownMessage(message);
            }

            protected override System.Threading.Tasks.Task<SharpRpc.IResponseMessage> OnRequest(SharpRpc.IRequestMessage request)
            {
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

            public PrebuiltMessages.CallbackMessage PrebuildCallbackMessage(System.Guid requestId, TestCommon.StressEntity entity)
            {
                Messages.C3_Message message = new Messages.C3_Message();
                message.Arg1 = requestId;
                message.Arg2 = entity;
                var bytes = _preserializer.SerializeOnSingleAdapter(message);
                return new PrebuiltMessages.CallbackMessage(bytes);
            }
        }

        public class PrebuiltMessages
        {
            public class CallbackMessage : SharpRpc.PrebuiltMessage
            {
                public CallbackMessage(SharpRpc.SegmentedByteArray bytes) : base(bytes)
                {
                }
            }
        }
    }
}