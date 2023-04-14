namespace TestCommon
{
    public class SyntaxTestContract_Gen
    {
        public static Client CreateClient(SharpRpc.ClientEndpoint endpoint, SharpRpc.SerializerChoice serializer = SharpRpc.SerializerChoice.MessagePack)
        {
            var adapter = CreateSerializationAdapter(serializer);
            var descriptor = CreateDescriptor(adapter);
            return new Client(endpoint, descriptor);
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
            public Client(SharpRpc.ClientEndpoint endpoint, SharpRpc.ContractDescriptor descriptor) : base(endpoint, descriptor)
            {
                Async = new AsyncFacade(Channel);
                Try = new TryFacade(Channel);
                TryAsync = new TryAsyncFacade(Channel);
            }

            public AsyncFacade Async { get; }

            public TryFacade Try { get; }

            public TryAsyncFacade TryAsync { get; }

            public SharpRpc.OutputStreamCall<System.Int32, int> OutStreamCall(SharpRpc.StreamOptions streamOptions, System.TimeSpan delay, int count, TestCommon.StreamTestOptions options)
            {
                Messages.C1_Request message = new Messages.C1_Request();
                message.Arg1 = delay;
                message.Arg2 = count;
                message.Arg3 = options;
                return OpenOutputStream<System.Int32, int>(message, streamOptions, new Messages.C1_OutputStreamFactory());
            }

            public SharpRpc.OutputStreamCall<System.Int32> OutStreamCallNoRet(SharpRpc.StreamOptions streamOptions, System.TimeSpan delay, int count, TestCommon.StreamTestOptions options)
            {
                Messages.C2_Request message = new Messages.C2_Request();
                message.Arg1 = delay;
                message.Arg2 = count;
                message.Arg3 = options;
                return OpenOutputStream<System.Int32>(message, streamOptions, new Messages.C2_OutputStreamFactory());
            }

            public SharpRpc.InputStreamCall<System.Int32, int> InStreamCall(SharpRpc.StreamOptions streamOptions, System.TimeSpan delay, TestCommon.StreamTestOptions options)
            {
                Messages.C3_Request message = new Messages.C3_Request();
                message.Arg1 = delay;
                message.Arg2 = options;
                return OpenInputStream<System.Int32, int>(message, streamOptions, new Messages.C3_InputStreamFactory());
            }

            public SharpRpc.InputStreamCall<System.Int32> InStreamCallNoRet(SharpRpc.StreamOptions streamOptions, System.TimeSpan delay, TestCommon.StreamTestOptions options)
            {
                Messages.C4_Request message = new Messages.C4_Request();
                message.Arg1 = delay;
                message.Arg2 = options;
                return OpenInputStream<System.Int32>(message, streamOptions, new Messages.C4_InputStreamFactory());
            }

            public SharpRpc.DuplexStreamCall<System.Int32, System.Int64, int> DuplexStreamCall(SharpRpc.DuplexStreamOptions streamOptions, System.TimeSpan delay, TestCommon.StreamTestOptions options)
            {
                Messages.C5_Request message = new Messages.C5_Request();
                message.Arg1 = delay;
                message.Arg2 = options;
                return OpenDuplexStream<System.Int32, System.Int64, int>(message, streamOptions, new Messages.C5_InputStreamFactory(), new Messages.C5_OutputStreamFactory());
            }

            public SharpRpc.DuplexStreamCall<System.Int32, System.Int64> tDuplexStreamCallNoRet(SharpRpc.DuplexStreamOptions streamOptions, System.TimeSpan delay, TestCommon.StreamTestOptions options)
            {
                Messages.C6_Request message = new Messages.C6_Request();
                message.Arg1 = delay;
                message.Arg2 = options;
                return OpenDuplexStream<System.Int32, System.Int64>(message, streamOptions, new Messages.C6_InputStreamFactory(), new Messages.C6_OutputStreamFactory());
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }
            }
        }

        public abstract class ServiceBase
        {
            public abstract System.Threading.Tasks.Task<int> OutStreamCall(SharpRpc.CallContext context, SharpRpc.StreamWriter<System.Int32> outputStream, System.TimeSpan delay, int count, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task OutStreamCallNoRet(SharpRpc.CallContext context, SharpRpc.StreamWriter<System.Int32> outputStream, System.TimeSpan delay, int count, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task<int> InStreamCall(SharpRpc.CallContext context, SharpRpc.StreamReader<System.Int32> inputStream, System.TimeSpan delay, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task InStreamCallNoRet(SharpRpc.CallContext context, SharpRpc.StreamReader<System.Int32> inputStream, System.TimeSpan delay, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task<int> DuplexStreamCall(SharpRpc.CallContext context, SharpRpc.StreamReader<System.Int32> inputStream, SharpRpc.StreamWriter<System.Int64> outputStream, System.TimeSpan delay, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task tDuplexStreamCallNoRet(SharpRpc.CallContext context, SharpRpc.StreamReader<System.Int32> inputStream, SharpRpc.StreamWriter<System.Int64> outputStream, System.TimeSpan delay, TestCommon.StreamTestOptions options);
            public SharpRpc.SessionInfo Session { get; private set; }

            public virtual void OnInit()
            {
            }

            public virtual void OnClose()
            {
            }

            public void InitServiceStub(SharpRpc.SessionInfo session)
            {
                Session = session;
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeOutStreamCall(Messages.C1_Request request)
            {
                var context = CreateOutputStreamContext<System.Int32>(request, new Messages.C1_OutputStreamFactory());
                try
                {
                    var result = await _stub.OutStreamCall(context, context.OutputStream, request.Arg1, request.Arg2, request.Arg3);
                    await CloseStreamContext(context);
                    var response = new Messages.C1_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C1_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C1_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeOutStreamCallNoRet(Messages.C2_Request request)
            {
                var context = CreateOutputStreamContext<System.Int32>(request, new Messages.C2_OutputStreamFactory());
                try
                {
                    await _stub.OutStreamCallNoRet(context, context.OutputStream, request.Arg1, request.Arg2, request.Arg3);
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeInStreamCall(Messages.C3_Request request)
            {
                var context = CreateInputStreamContext<System.Int32>(request, new Messages.C3_InputStreamFactory());
                try
                {
                    var result = await _stub.InStreamCall(context, context.InputStream, request.Arg1, request.Arg2);
                    await CloseStreamContext(context);
                    var response = new Messages.C3_Response();
                    response.Result = result;
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeInStreamCallNoRet(Messages.C4_Request request)
            {
                var context = CreateInputStreamContext<System.Int32>(request, new Messages.C4_InputStreamFactory());
                try
                {
                    await _stub.InStreamCallNoRet(context, context.InputStream, request.Arg1, request.Arg2);
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeDuplexStreamCall(Messages.C5_Request request)
            {
                var context = CreateDuplexStreamContext<System.Int32, System.Int64>(request, new Messages.C5_InputStreamFactory(), new Messages.C5_OutputStreamFactory());
                try
                {
                    var result = await _stub.DuplexStreamCall(context, context.InputStream, context.OutputStream, request.Arg1, request.Arg2);
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvoketDuplexStreamCallNoRet(Messages.C6_Request request)
            {
                var context = CreateDuplexStreamContext<System.Int32, System.Int64>(request, new Messages.C6_InputStreamFactory(), new Messages.C6_OutputStreamFactory());
                try
                {
                    await _stub.tDuplexStreamCallNoRet(context, context.InputStream, context.OutputStream, request.Arg1, request.Arg2);
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
                    var r5 = (Messages.C6_Request)request;
                    return InvoketDuplexStreamCallNoRet(r5);
                }
                else if (request is Messages.C5_Request)
                {
                    var r4 = (Messages.C5_Request)request;
                    return InvokeDuplexStreamCall(r4);
                }
                else if (request is Messages.C4_Request)
                {
                    var r3 = (Messages.C4_Request)request;
                    return InvokeInStreamCallNoRet(r3);
                }
                else if (request is Messages.C3_Request)
                {
                    var r2 = (Messages.C3_Request)request;
                    return InvokeInStreamCall(r2);
                }
                else if (request is Messages.C2_Request)
                {
                    var r1 = (Messages.C2_Request)request;
                    return InvokeOutStreamCallNoRet(r1);
                }
                else if (request is Messages.C1_Request)
                {
                    var r0 = (Messages.C1_Request)request;
                    return InvokeOutStreamCall(r0);
                }
                else
                    return OnUnknownRequest(request);
            }

            protected override void OnInit(SharpRpc.Channel channel)
            {
                _stub.InitServiceStub(Session);
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
                MessagePack.MessagePackSerializer.Serialize<TestCommon.SyntaxTestContract_Gen.Messages.MessageBase>(writer.ByteStream, (TestCommon.SyntaxTestContract_Gen.Messages.MessageBase)message);
            }

            public SharpRpc.IMessage Deserialize(SharpRpc.MessageReader reader)
            {
                return MessagePack.MessagePackSerializer.Deserialize<TestCommon.SyntaxTestContract_Gen.Messages.MessageBase>(reader.ByteStream);
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
            [MessagePack.UnionAttribute(39, typeof(Messages.C1_OutputPage))]
            [MessagePack.UnionAttribute(40, typeof(Messages.C2_Request))]
            [MessagePack.UnionAttribute(41, typeof(Messages.C2_Response))]
            [MessagePack.UnionAttribute(42, typeof(Messages.C2_Fault))]
            [MessagePack.UnionAttribute(44, typeof(Messages.C2_OutputPage))]
            [MessagePack.UnionAttribute(45, typeof(Messages.C3_Request))]
            [MessagePack.UnionAttribute(46, typeof(Messages.C3_Response))]
            [MessagePack.UnionAttribute(47, typeof(Messages.C3_Fault))]
            [MessagePack.UnionAttribute(48, typeof(Messages.C3_InputPage))]
            [MessagePack.UnionAttribute(50, typeof(Messages.C4_Request))]
            [MessagePack.UnionAttribute(51, typeof(Messages.C4_Response))]
            [MessagePack.UnionAttribute(52, typeof(Messages.C4_Fault))]
            [MessagePack.UnionAttribute(53, typeof(Messages.C4_InputPage))]
            [MessagePack.UnionAttribute(55, typeof(Messages.C5_Request))]
            [MessagePack.UnionAttribute(56, typeof(Messages.C5_Response))]
            [MessagePack.UnionAttribute(57, typeof(Messages.C5_Fault))]
            [MessagePack.UnionAttribute(58, typeof(Messages.C5_InputPage))]
            [MessagePack.UnionAttribute(59, typeof(Messages.C5_OutputPage))]
            [MessagePack.UnionAttribute(60, typeof(Messages.C6_Request))]
            [MessagePack.UnionAttribute(61, typeof(Messages.C6_Response))]
            [MessagePack.UnionAttribute(62, typeof(Messages.C6_Fault))]
            [MessagePack.UnionAttribute(63, typeof(Messages.C6_InputPage))]
            [MessagePack.UnionAttribute(64, typeof(Messages.C6_OutputPage))]
            public interface MessageBase : global::SharpRpc.IMessage
            {
            }

            public class C1_OutputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C1_OutputPage(streamId);
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

            public class C2_OutputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C2_OutputPage(streamId);
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

            public class C3_InputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C3_InputPage(streamId);
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

            public class C4_InputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C4_InputPage(streamId);
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

            public class C5_InputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
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

            public class C5_OutputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int64>
            {
                public SharpRpc.IStreamPage<System.Int64> CreatePage(string streamId)
                {
                    return new C5_OutputPage(streamId);
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

            public class C6_InputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
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

            public class C6_OutputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int64>
            {
                public SharpRpc.IStreamPage<System.Int64> CreatePage(string streamId)
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
            public class Login : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.ILoginMessage
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
            public class Logout : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.ILogoutMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class Heartbeat : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IHeartbeatMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class PageAcknowledgement : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPageAck
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
            public class CancelStream : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCancelMessage
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
            public class CloseStream : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseMessage
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
            public class CloseStreamAck : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseAckMessage
            {
                public CloseStreamAck(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CancelRequest : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.ICancelRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public ushort? WindowSize { get; set; }

                [MessagePack.KeyAttribute(3)]
                public System.TimeSpan Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public int Arg2 { get; set; }

                [MessagePack.KeyAttribute(5)]
                public TestCommon.StreamTestOptions Arg3 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Response : MessageBase, SharpRpc.IResponseMessage<int>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public int Result { get; set; }
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
            public class C1_OutputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C1_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
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

                [MessagePack.KeyAttribute(3)]
                public System.TimeSpan Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public int Arg2 { get; set; }

                [MessagePack.KeyAttribute(5)]
                public TestCommon.StreamTestOptions Arg3 { get; set; }
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
            public class C2_OutputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C2_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
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

                [MessagePack.KeyAttribute(3)]
                public System.TimeSpan Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public TestCommon.StreamTestOptions Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C3_Response : MessageBase, SharpRpc.IResponseMessage<int>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public int Result { get; set; }
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
            public class C3_InputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C3_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
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
                public System.TimeSpan Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public TestCommon.StreamTestOptions Arg2 { get; set; }
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
            public class C4_InputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C4_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
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
                public System.TimeSpan Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public TestCommon.StreamTestOptions Arg2 { get; set; }
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
            public class C5_InputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C5_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_OutputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int64>
            {
                public C5_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int64> Items { get; set; }
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
                public System.TimeSpan Arg1 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public TestCommon.StreamTestOptions Arg2 { get; set; }
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
            public class C6_InputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C6_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_OutputPage : TestCommon.SyntaxTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int64>
            {
                public C6_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int64> Items { get; set; }
            }
        }
    }
}