namespace TestCommon
{
    public class FunctionTestContract_Gen
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

            public void TestNotify1(int p1, string p2)
            {
                Messages.C0_Message message = new Messages.C0_Message();
                message.Arg1 = p1;
                message.Arg2 = p2;
                SendMessage(message);
            }

            public void TestCall1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C1_Request message = new Messages.C1_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                CallAsync<Messages.C1_Response>(message, cancelToken).Wait();
            }

            public string TestCall2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C2_Request message = new Messages.C2_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                return CallAsync<string, Messages.C2_Response>(message, cancelToken).Result;
            }

            public string TestCrash(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C3_Request message = new Messages.C3_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                return CallAsync<string, Messages.C3_Response>(message, cancelToken).Result;
            }

            public string TestRpcException(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C4_Request message = new Messages.C4_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                return CallAsync<string, Messages.C4_Response>(message, cancelToken).Result;
            }

            public void TestCallFault(int faultNo, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C5_Request message = new Messages.C5_Request();
                message.Arg1 = faultNo;
                CallAsync<Messages.C5_Response>(message, cancelToken).Wait();
            }

            public string InvokeCallback(int callbackNo, int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C6_Request message = new Messages.C6_Request();
                message.Arg1 = callbackNo;
                message.Arg2 = p1;
                message.Arg3 = p2;
                return CallAsync<string, Messages.C6_Response>(message, cancelToken).Result;
            }

            public System.Collections.Generic.List<System.Tuple<int>> ComplexTypesCall(System.Collections.Generic.List<System.DateTime> list, System.Collections.Generic.List<System.Collections.Generic.List<System.DateTime>> listOfLists, System.Collections.Generic.Dictionary<int, int> dictionary, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C11_Request message = new Messages.C11_Request();
                message.Arg1 = list;
                message.Arg2 = listOfLists;
                message.Arg3 = dictionary;
                return CallAsync<System.Collections.Generic.List<System.Tuple<int>>, Messages.C11_Response>(message, cancelToken).Result;
            }

            public SharpRpc.OutputStreamCall<System.Int32, TestCommon.StreamCallResult> TestOutStream(SharpRpc.StreamOptions streamOptions, System.TimeSpan delay, int count, TestCommon.StreamTestOptions options)
            {
                Messages.C12_Request message = new Messages.C12_Request();
                message.Arg1 = delay;
                message.Arg2 = count;
                message.Arg3 = options;
                return OpenOutputStream<System.Int32, TestCommon.StreamCallResult>(message, streamOptions, new Messages.C12_OutputStreamFactory());
            }

            public SharpRpc.InputStreamCall<System.Int32, TestCommon.StreamCallResult> TestInStream(SharpRpc.StreamOptions streamOptions, System.TimeSpan delay, TestCommon.StreamTestOptions options)
            {
                Messages.C13_Request message = new Messages.C13_Request();
                message.Arg1 = delay;
                message.Arg2 = options;
                return OpenInputStream<System.Int32, TestCommon.StreamCallResult>(message, streamOptions, new Messages.C13_InputStreamFactory());
            }

            public SharpRpc.DuplexStreamCall<System.Int32, System.Int32, int> TestDuplexStream(SharpRpc.DuplexStreamOptions streamOptions, System.TimeSpan delay, TestCommon.StreamTestOptions options)
            {
                Messages.C14_Request message = new Messages.C14_Request();
                message.Arg1 = delay;
                message.Arg2 = options;
                return OpenDuplexStream<System.Int32, System.Int32, int>(message, streamOptions, new Messages.C14_InputStreamFactory(), new Messages.C14_OutputStreamFactory());
            }

            public bool CancellableCall(System.TimeSpan delay, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C15_Request message = new Messages.C15_Request();
                message.Arg1 = delay;
                return CallAsync<bool, Messages.C15_Response>(message, cancelToken).Result;
            }

            public string GetSessionSharedProperty(string name, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C16_Request message = new Messages.C16_Request();
                message.Arg1 = name;
                return CallAsync<string, Messages.C16_Response>(message, cancelToken).Result;
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task TestNotify1(int p1, string p2)
                {
                    Messages.C0_Message message = new Messages.C0_Message();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task TestCall1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<Messages.C1_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<string> TestCall2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C2_Request message = new Messages.C2_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<string, Messages.C2_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<string> TestCrash(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C3_Request message = new Messages.C3_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<string, Messages.C3_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<string> TestRpcException(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C4_Request message = new Messages.C4_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<string, Messages.C4_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task TestCallFault(int faultNo, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C5_Request message = new Messages.C5_Request();
                    message.Arg1 = faultNo;
                    return CallAsync<Messages.C5_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<string> InvokeCallback(int callbackNo, int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C6_Request message = new Messages.C6_Request();
                    message.Arg1 = callbackNo;
                    message.Arg2 = p1;
                    message.Arg3 = p2;
                    return CallAsync<string, Messages.C6_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<System.Collections.Generic.List<System.Tuple<int>>> ComplexTypesCall(System.Collections.Generic.List<System.DateTime> list, System.Collections.Generic.List<System.Collections.Generic.List<System.DateTime>> listOfLists, System.Collections.Generic.Dictionary<int, int> dictionary, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C11_Request message = new Messages.C11_Request();
                    message.Arg1 = list;
                    message.Arg2 = listOfLists;
                    message.Arg3 = dictionary;
                    return CallAsync<System.Collections.Generic.List<System.Tuple<int>>, Messages.C11_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<bool> CancellableCall(System.TimeSpan delay, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C15_Request message = new Messages.C15_Request();
                    message.Arg1 = delay;
                    return CallAsync<bool, Messages.C15_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<string> GetSessionSharedProperty(string name, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C16_Request message = new Messages.C16_Request();
                    message.Arg1 = name;
                    return CallAsync<string, Messages.C16_Response>(message, cancelToken);
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public SharpRpc.RpcResult TestNotify1(int p1, string p2)
                {
                    Messages.C0_Message message = new Messages.C0_Message();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult TestCall1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<Messages.C1_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<string> TestCall2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C2_Request message = new Messages.C2_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C2_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<string> TestCrash(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C3_Request message = new Messages.C3_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C3_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<string> TestRpcException(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C4_Request message = new Messages.C4_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C4_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult TestCallFault(int faultNo, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C5_Request message = new Messages.C5_Request();
                    message.Arg1 = faultNo;
                    return TryCallAsync<Messages.C5_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<string> InvokeCallback(int callbackNo, int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C6_Request message = new Messages.C6_Request();
                    message.Arg1 = callbackNo;
                    message.Arg2 = p1;
                    message.Arg3 = p2;
                    return TryCallAsync<string, Messages.C6_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<System.Collections.Generic.List<System.Tuple<int>>> ComplexTypesCall(System.Collections.Generic.List<System.DateTime> list, System.Collections.Generic.List<System.Collections.Generic.List<System.DateTime>> listOfLists, System.Collections.Generic.Dictionary<int, int> dictionary, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C11_Request message = new Messages.C11_Request();
                    message.Arg1 = list;
                    message.Arg2 = listOfLists;
                    message.Arg3 = dictionary;
                    return TryCallAsync<System.Collections.Generic.List<System.Tuple<int>>, Messages.C11_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<bool> CancellableCall(System.TimeSpan delay, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C15_Request message = new Messages.C15_Request();
                    message.Arg1 = delay;
                    return TryCallAsync<bool, Messages.C15_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<string> GetSessionSharedProperty(string name, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C16_Request message = new Messages.C16_Request();
                    message.Arg1 = name;
                    return TryCallAsync<string, Messages.C16_Response>(message, cancelToken).Result;
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> TestNotify1(int p1, string p2)
                {
                    Messages.C0_Message message = new Messages.C0_Message();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> TestCall1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C1_Request message = new Messages.C1_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<Messages.C1_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<string>> TestCall2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C2_Request message = new Messages.C2_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C2_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<string>> TestCrash(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C3_Request message = new Messages.C3_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C3_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<string>> TestRpcException(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C4_Request message = new Messages.C4_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C4_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> TestCallFault(int faultNo, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C5_Request message = new Messages.C5_Request();
                    message.Arg1 = faultNo;
                    return TryCallAsync<Messages.C5_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<string>> InvokeCallback(int callbackNo, int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C6_Request message = new Messages.C6_Request();
                    message.Arg1 = callbackNo;
                    message.Arg2 = p1;
                    message.Arg3 = p2;
                    return TryCallAsync<string, Messages.C6_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<System.Collections.Generic.List<System.Tuple<int>>>> ComplexTypesCall(System.Collections.Generic.List<System.DateTime> list, System.Collections.Generic.List<System.Collections.Generic.List<System.DateTime>> listOfLists, System.Collections.Generic.Dictionary<int, int> dictionary, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C11_Request message = new Messages.C11_Request();
                    message.Arg1 = list;
                    message.Arg2 = listOfLists;
                    message.Arg3 = dictionary;
                    return TryCallAsync<System.Collections.Generic.List<System.Tuple<int>>, Messages.C11_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<bool>> CancellableCall(System.TimeSpan delay, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C15_Request message = new Messages.C15_Request();
                    message.Arg1 = delay;
                    return TryCallAsync<bool, Messages.C15_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<string>> GetSessionSharedProperty(string name, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C16_Request message = new Messages.C16_Request();
                    message.Arg1 = name;
                    return TryCallAsync<string, Messages.C16_Response>(message, cancelToken);
                }
            }
        }

        public abstract class ServiceBase
        {
            public abstract System.Threading.Tasks.Task TestNotify1(int p1, string p2);
            public abstract System.Threading.Tasks.Task TestCall1(SharpRpc.CallContext context, int p1, string p2);
            public abstract System.Threading.Tasks.Task<string> TestCall2(SharpRpc.CallContext context, int p1, string p2);
            public abstract System.Threading.Tasks.Task<string> TestCrash(SharpRpc.CallContext context, int p1, string p2);
            public abstract System.Threading.Tasks.Task<string> TestRpcException(SharpRpc.CallContext context, int p1, string p2);
            public abstract System.Threading.Tasks.Task TestCallFault(SharpRpc.CallContext context, int faultNo);
            public abstract System.Threading.Tasks.Task<string> InvokeCallback(SharpRpc.CallContext context, int callbackNo, int p1, string p2);
            public abstract System.Threading.Tasks.Task<System.Collections.Generic.List<System.Tuple<int>>> ComplexTypesCall(SharpRpc.CallContext context, System.Collections.Generic.List<System.DateTime> list, System.Collections.Generic.List<System.Collections.Generic.List<System.DateTime>> listOfLists, System.Collections.Generic.Dictionary<int, int> dictionary);
            public abstract System.Threading.Tasks.Task<TestCommon.StreamCallResult> TestOutStream(SharpRpc.CallContext context, SharpRpc.StreamWriter<System.Int32> outputStream, System.TimeSpan delay, int count, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task<TestCommon.StreamCallResult> TestInStream(SharpRpc.CallContext context, SharpRpc.StreamReader<System.Int32> inputStream, System.TimeSpan delay, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task<int> TestDuplexStream(SharpRpc.CallContext context, SharpRpc.StreamReader<System.Int32> inputStream, SharpRpc.StreamWriter<System.Int32> outputStream, System.TimeSpan delay, TestCommon.StreamTestOptions options);
            public abstract System.Threading.Tasks.Task<bool> CancellableCall(SharpRpc.CallContext context, System.TimeSpan delay);
            public abstract System.Threading.Tasks.Task<string> GetSessionSharedProperty(SharpRpc.CallContext context, string name);
            public virtual void OnResponseSent_TestCall2(string responseValue)
            {
            }

            public virtual void OnResponseSent_TestCrash(string responseValue)
            {
            }

            public virtual void OnResponseSent_TestRpcException(string responseValue)
            {
            }

            public virtual void OnResponseSent_InvokeCallback(string responseValue)
            {
            }

            public virtual void OnResponseSent_ComplexTypesCall(System.Collections.Generic.List<System.Tuple<int>> responseValue)
            {
            }

            public virtual void OnResponseSent_TestOutStream(TestCommon.StreamCallResult responseValue)
            {
            }

            public virtual void OnResponseSent_TestInStream(TestCommon.StreamCallResult responseValue)
            {
            }

            public virtual void OnResponseSent_TestDuplexStream(int responseValue)
            {
            }

            public virtual void OnResponseSent_CancellableCall(bool responseValue)
            {
            }

            public virtual void OnResponseSent_GetSessionSharedProperty(string responseValue)
            {
            }

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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCall1(Messages.C1_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.TestCall1(context, request.Arg1, request.Arg2);
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCall2(Messages.C2_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.TestCall2(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C2_Response();
                    response.Result = result;
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCrash(Messages.C3_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.TestCrash(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C3_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C3_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C3_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestRpcException(Messages.C4_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.TestRpcException(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C4_Response();
                    response.Result = result;
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCallFault(Messages.C5_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.TestCallFault(context, request.Arg1);
                    CloseContext(context);
                    var response = new Messages.C5_Response();
                    return response;
                }
                catch (SharpRpc.RpcFaultException<TestCommon.TestFault1> ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C5_Fault();
                    faultMsg.CustomFaultBinding = new Messages.C5_Fault.F0_Adapter
                    {
                        Data = ex.Fault
                    };
                    return OnCustomFault(faultMsg, ex.Message);
                }
                catch (SharpRpc.RpcFaultException<TestCommon.TestFault2> ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C5_Fault();
                    faultMsg.CustomFaultBinding = new Messages.C5_Fault.F1_Adapter
                    {
                        Data = ex.Fault
                    };
                    return OnCustomFault(faultMsg, ex.Message);
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeInvokeCallback(Messages.C6_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.InvokeCallback(context, request.Arg1, request.Arg2, request.Arg3);
                    CloseContext(context);
                    var response = new Messages.C6_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C6_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C6_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeComplexTypesCall(Messages.C11_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.ComplexTypesCall(context, request.Arg1, request.Arg2, request.Arg3);
                    CloseContext(context);
                    var response = new Messages.C11_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C11_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C11_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestOutStream(Messages.C12_Request request)
            {
                var context = CreateOutputStreamContext<System.Int32>(request, new Messages.C12_OutputStreamFactory());
                try
                {
                    var result = await _stub.TestOutStream(context, context.OutputStream, request.Arg1, request.Arg2, request.Arg3);
                    await CloseStreamContext(context);
                    var response = new Messages.C12_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C12_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C12_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestInStream(Messages.C13_Request request)
            {
                var context = CreateInputStreamContext<System.Int32>(request, new Messages.C13_InputStreamFactory());
                try
                {
                    var result = await _stub.TestInStream(context, context.InputStream, request.Arg1, request.Arg2);
                    await CloseStreamContext(context);
                    var response = new Messages.C13_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C13_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C13_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestDuplexStream(Messages.C14_Request request)
            {
                var context = CreateDuplexStreamContext<System.Int32, System.Int32>(request, new Messages.C14_InputStreamFactory(), new Messages.C14_OutputStreamFactory());
                try
                {
                    var result = await _stub.TestDuplexStream(context, context.InputStream, context.OutputStream, request.Arg1, request.Arg2);
                    await CloseStreamContext(context);
                    var response = new Messages.C14_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C14_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    await CloseStreamContext(context);
                    var faultMsg = new Messages.C14_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeCancellableCall(Messages.C15_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.CancellableCall(context, request.Arg1);
                    CloseContext(context);
                    var response = new Messages.C15_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C15_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C15_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeGetSessionSharedProperty(Messages.C16_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.GetSessionSharedProperty(context, request.Arg1);
                    CloseContext(context);
                    var response = new Messages.C16_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C16_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C16_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            protected override System.Threading.Tasks.Task OnMessage(SharpRpc.IMessage message)
            {
                if (message is Messages.C0_Message)
                {
                    var m0 = (Messages.C0_Message)message;
                    return _stub.TestNotify1(m0.Arg1, m0.Arg2);
                }
                else
                    return OnUnknownMessage(message);
            }

            protected override System.Threading.Tasks.Task<SharpRpc.IResponseMessage> OnRequest(SharpRpc.IRequestMessage request)
            {
                if (request is Messages.C16_Request)
                {
                    var r11 = (Messages.C16_Request)request;
                    return InvokeGetSessionSharedProperty(r11);
                }
                else if (request is Messages.C15_Request)
                {
                    var r10 = (Messages.C15_Request)request;
                    return InvokeCancellableCall(r10);
                }
                else if (request is Messages.C14_Request)
                {
                    var r9 = (Messages.C14_Request)request;
                    return InvokeTestDuplexStream(r9);
                }
                else if (request is Messages.C13_Request)
                {
                    var r8 = (Messages.C13_Request)request;
                    return InvokeTestInStream(r8);
                }
                else if (request is Messages.C12_Request)
                {
                    var r7 = (Messages.C12_Request)request;
                    return InvokeTestOutStream(r7);
                }
                else if (request is Messages.C11_Request)
                {
                    var r6 = (Messages.C11_Request)request;
                    return InvokeComplexTypesCall(r6);
                }
                else if (request is Messages.C6_Request)
                {
                    var r5 = (Messages.C6_Request)request;
                    return InvokeInvokeCallback(r5);
                }
                else if (request is Messages.C5_Request)
                {
                    var r4 = (Messages.C5_Request)request;
                    return InvokeTestCallFault(r4);
                }
                else if (request is Messages.C4_Request)
                {
                    var r3 = (Messages.C4_Request)request;
                    return InvokeTestRpcException(r3);
                }
                else if (request is Messages.C3_Request)
                {
                    var r2 = (Messages.C3_Request)request;
                    return InvokeTestCrash(r2);
                }
                else if (request is Messages.C2_Request)
                {
                    var r1 = (Messages.C2_Request)request;
                    return InvokeTestCall2(r1);
                }
                else if (request is Messages.C1_Request)
                {
                    var r0 = (Messages.C1_Request)request;
                    return InvokeTestCall1(r0);
                }
                else
                    return OnUnknownRequest(request);
            }

            protected override void OnResponseSent(SharpRpc.IResponseMessage response)
            {
                if (response is Messages.C16_Response)
                {
                    var r9 = (Messages.C16_Response)response;
                    _stub.OnResponseSent_GetSessionSharedProperty(r9.Result);
                }
                else if (response is Messages.C15_Response)
                {
                    var r8 = (Messages.C15_Response)response;
                    _stub.OnResponseSent_CancellableCall(r8.Result);
                }
                else if (response is Messages.C14_Response)
                {
                    var r7 = (Messages.C14_Response)response;
                    _stub.OnResponseSent_TestDuplexStream(r7.Result);
                }
                else if (response is Messages.C13_Response)
                {
                    var r6 = (Messages.C13_Response)response;
                    _stub.OnResponseSent_TestInStream(r6.Result);
                }
                else if (response is Messages.C12_Response)
                {
                    var r5 = (Messages.C12_Response)response;
                    _stub.OnResponseSent_TestOutStream(r5.Result);
                }
                else if (response is Messages.C11_Response)
                {
                    var r4 = (Messages.C11_Response)response;
                    _stub.OnResponseSent_ComplexTypesCall(r4.Result);
                }
                else if (response is Messages.C6_Response)
                {
                    var r3 = (Messages.C6_Response)response;
                    _stub.OnResponseSent_InvokeCallback(r3.Result);
                }
                else if (response is Messages.C4_Response)
                {
                    var r2 = (Messages.C4_Response)response;
                    _stub.OnResponseSent_TestRpcException(r2.Result);
                }
                else if (response is Messages.C3_Response)
                {
                    var r1 = (Messages.C3_Response)response;
                    _stub.OnResponseSent_TestCrash(r1.Result);
                }
                else if (response is Messages.C2_Response)
                {
                    var r0 = (Messages.C2_Response)response;
                    _stub.OnResponseSent_TestCall2(r0.Result);
                }
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
                MessagePack.MessagePackSerializer.Serialize<TestCommon.FunctionTestContract_Gen.Messages.MessageBase>(writer.ByteStream, (TestCommon.FunctionTestContract_Gen.Messages.MessageBase)message);
            }

            public SharpRpc.IMessage Deserialize(SharpRpc.MessageReader reader)
            {
                return MessagePack.MessagePackSerializer.Deserialize<TestCommon.FunctionTestContract_Gen.Messages.MessageBase>(reader.ByteStream);
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
            [MessagePack.UnionAttribute(45, typeof(Messages.C3_Request))]
            [MessagePack.UnionAttribute(46, typeof(Messages.C3_Response))]
            [MessagePack.UnionAttribute(47, typeof(Messages.C3_Fault))]
            [MessagePack.UnionAttribute(50, typeof(Messages.C4_Request))]
            [MessagePack.UnionAttribute(51, typeof(Messages.C4_Response))]
            [MessagePack.UnionAttribute(52, typeof(Messages.C4_Fault))]
            [MessagePack.UnionAttribute(55, typeof(Messages.C5_Request))]
            [MessagePack.UnionAttribute(56, typeof(Messages.C5_Response))]
            [MessagePack.UnionAttribute(57, typeof(Messages.C5_Fault))]
            [MessagePack.UnionAttribute(60, typeof(Messages.C6_Request))]
            [MessagePack.UnionAttribute(61, typeof(Messages.C6_Response))]
            [MessagePack.UnionAttribute(62, typeof(Messages.C6_Fault))]
            [MessagePack.UnionAttribute(65, typeof(Messages.C7_Message))]
            [MessagePack.UnionAttribute(70, typeof(Messages.C8_Request))]
            [MessagePack.UnionAttribute(71, typeof(Messages.C8_Response))]
            [MessagePack.UnionAttribute(72, typeof(Messages.C8_Fault))]
            [MessagePack.UnionAttribute(75, typeof(Messages.C9_Request))]
            [MessagePack.UnionAttribute(76, typeof(Messages.C9_Response))]
            [MessagePack.UnionAttribute(77, typeof(Messages.C9_Fault))]
            [MessagePack.UnionAttribute(80, typeof(Messages.C10_Request))]
            [MessagePack.UnionAttribute(81, typeof(Messages.C10_Response))]
            [MessagePack.UnionAttribute(82, typeof(Messages.C10_Fault))]
            [MessagePack.UnionAttribute(85, typeof(Messages.C11_Request))]
            [MessagePack.UnionAttribute(86, typeof(Messages.C11_Response))]
            [MessagePack.UnionAttribute(87, typeof(Messages.C11_Fault))]
            [MessagePack.UnionAttribute(90, typeof(Messages.C12_Request))]
            [MessagePack.UnionAttribute(91, typeof(Messages.C12_Response))]
            [MessagePack.UnionAttribute(92, typeof(Messages.C12_Fault))]
            [MessagePack.UnionAttribute(94, typeof(Messages.C12_OutputPage))]
            [MessagePack.UnionAttribute(95, typeof(Messages.C13_Request))]
            [MessagePack.UnionAttribute(96, typeof(Messages.C13_Response))]
            [MessagePack.UnionAttribute(97, typeof(Messages.C13_Fault))]
            [MessagePack.UnionAttribute(98, typeof(Messages.C13_InputPage))]
            [MessagePack.UnionAttribute(100, typeof(Messages.C14_Request))]
            [MessagePack.UnionAttribute(101, typeof(Messages.C14_Response))]
            [MessagePack.UnionAttribute(102, typeof(Messages.C14_Fault))]
            [MessagePack.UnionAttribute(103, typeof(Messages.C14_InputPage))]
            [MessagePack.UnionAttribute(104, typeof(Messages.C14_OutputPage))]
            [MessagePack.UnionAttribute(105, typeof(Messages.C15_Request))]
            [MessagePack.UnionAttribute(106, typeof(Messages.C15_Response))]
            [MessagePack.UnionAttribute(107, typeof(Messages.C15_Fault))]
            [MessagePack.UnionAttribute(110, typeof(Messages.C16_Request))]
            [MessagePack.UnionAttribute(111, typeof(Messages.C16_Response))]
            [MessagePack.UnionAttribute(112, typeof(Messages.C16_Fault))]
            public interface MessageBase : global::SharpRpc.IMessage
            {
            }

            public class C12_OutputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C12_OutputPage(streamId);
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

            public class C13_InputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C13_InputPage(streamId);
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

            public class C14_InputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C14_InputPage(streamId);
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

            public class C14_OutputStreamFactory : SharpRpc.IStreamMessageFactory<System.Int32>
            {
                public SharpRpc.IStreamPage<System.Int32> CreatePage(string streamId)
                {
                    return new C14_OutputPage(streamId);
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
            public class Login : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.ILoginMessage
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
            public class Logout : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.ILogoutMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class Heartbeat : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IHeartbeatMessage
            {
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class PageAcknowledgement : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPageAck
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
            public class CancelStream : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCancelMessage
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
            public class CloseStream : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseMessage
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
            public class CloseStreamAck : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamCloseAckMessage
            {
                public CloseStreamAck(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class CancelRequest : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.ICancelRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C0_Message : MessageBase
            {
                [MessagePack.KeyAttribute(0)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C1_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
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
            public class C2_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C2_Response : MessageBase, SharpRpc.IResponseMessage<string>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Result { get; set; }
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
            public class C3_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C3_Response : MessageBase, SharpRpc.IResponseMessage<string>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Result { get; set; }
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
            public class C4_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C4_Response : MessageBase, SharpRpc.IResponseMessage<string>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Result { get; set; }
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
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C5_Fault : MessageBase, SharpRpc.IRequestFaultMessage
            {
                public SharpRpc.ICustomFaultBinding GetCustomFaultBinding()
                {
                    return CustomFaultBinding;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Text { get; set; }

                [MessagePack.KeyAttribute(2)]
                public SharpRpc.RequestFaultCode Code { get; set; }

                [MessagePack.KeyAttribute(3)]
                public IFaultAdapter CustomFaultBinding { get; set; }

                [MessagePack.UnionAttribute(0, typeof(Messages.C5_Fault.F0_Adapter))]
                [MessagePack.UnionAttribute(1, typeof(Messages.C5_Fault.F1_Adapter))]
                public interface IFaultAdapter : SharpRpc.ICustomFaultBinding
                {
                }

                [MessagePack.MessagePackObjectAttribute()]
                public class F0_Adapter : IFaultAdapter
                {
                    [MessagePack.KeyAttribute(0)]
                    public TestCommon.TestFault1 Data { get; set; }

                    public object GetFault()
                    {
                        return Data;
                    }

                    public SharpRpc.RpcFaultException CreateException(string text)
                    {
                        return new SharpRpc.RpcFaultException<TestCommon.TestFault1>(text, Data);
                    }
                }

                [MessagePack.MessagePackObjectAttribute()]
                public class F1_Adapter : IFaultAdapter
                {
                    [MessagePack.KeyAttribute(0)]
                    public TestCommon.TestFault2 Data { get; set; }

                    public object GetFault()
                    {
                        return Data;
                    }

                    public SharpRpc.RpcFaultException CreateException(string text)
                    {
                        return new SharpRpc.RpcFaultException<TestCommon.TestFault2>(text, Data);
                    }
                }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public int Arg2 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public string Arg3 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C6_Response : MessageBase, SharpRpc.IResponseMessage<string>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Result { get; set; }
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
            public class C7_Message : MessageBase
            {
                [MessagePack.KeyAttribute(0)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C8_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C8_Response : MessageBase, global::SharpRpc.IResponseMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }
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

            [MessagePack.MessagePackObjectAttribute()]
            public class C9_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C9_Response : MessageBase, SharpRpc.IResponseMessage<int>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public int Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C9_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C10_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public int Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public string Arg2 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C10_Response : MessageBase, SharpRpc.IResponseMessage<string>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C10_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C11_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public System.Collections.Generic.List<System.DateTime> Arg1 { get; set; }

                [MessagePack.KeyAttribute(3)]
                public System.Collections.Generic.List<System.Collections.Generic.List<System.DateTime>> Arg2 { get; set; }

                [MessagePack.KeyAttribute(4)]
                public System.Collections.Generic.Dictionary<int, int> Arg3 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C11_Response : MessageBase, SharpRpc.IResponseMessage<System.Collections.Generic.List<System.Tuple<int>>>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Tuple<int>> Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C11_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C12_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
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
            public class C12_Response : MessageBase, SharpRpc.IResponseMessage<TestCommon.StreamCallResult>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public TestCommon.StreamCallResult Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C12_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C12_OutputPage : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C12_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C13_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
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
            public class C13_Response : MessageBase, SharpRpc.IResponseMessage<TestCommon.StreamCallResult>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public TestCommon.StreamCallResult Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C13_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C13_InputPage : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C13_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C14_Request : MessageBase, global::SharpRpc.IOpenStreamRequest
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
            public class C14_Response : MessageBase, SharpRpc.IResponseMessage<int>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public int Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C14_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C14_InputPage : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C14_InputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C14_OutputPage : TestCommon.FunctionTestContract_Gen.Messages.MessageBase, SharpRpc.IStreamPage<System.Int32>
            {
                public C14_OutputPage(string streamId)
                {
                    CallId = streamId;
                }

                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public System.Collections.Generic.List<System.Int32> Items { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C15_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public System.TimeSpan Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C15_Response : MessageBase, SharpRpc.IResponseMessage<bool>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public bool Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C15_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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
            public class C16_Request : MessageBase, global::SharpRpc.IRequestMessage
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public SharpRpc.RequestOptions Options { get; set; }

                [MessagePack.KeyAttribute(2)]
                public string Arg1 { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C16_Response : MessageBase, SharpRpc.IResponseMessage<string>
            {
                [MessagePack.KeyAttribute(0)]
                public string CallId { get; set; }

                [MessagePack.KeyAttribute(1)]
                public string Result { get; set; }
            }

            [MessagePack.MessagePackObjectAttribute()]
            public class C16_Fault : MessageBase, SharpRpc.IRequestFaultMessage
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

            public void TestCallbackNotify1(int p1, string p2)
            {
                Messages.C7_Message message = new Messages.C7_Message();
                message.Arg1 = p1;
                message.Arg2 = p2;
                SendMessage(message);
            }

            public void TestCallback1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C8_Request message = new Messages.C8_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                CallAsync<Messages.C8_Response>(message, cancelToken).Wait();
            }

            public int TestCallback2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C9_Request message = new Messages.C9_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                return CallAsync<int, Messages.C9_Response>(message, cancelToken).Result;
            }

            public string TestCallback3(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
            {
                Messages.C10_Request message = new Messages.C10_Request();
                message.Arg1 = p1;
                message.Arg2 = p2;
                return CallAsync<string, Messages.C10_Response>(message, cancelToken).Result;
            }

            public class AsyncFacade : SharpRpc.ClientFacadeBase
            {
                public AsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task TestCallbackNotify1(int p1, string p2)
                {
                    Messages.C7_Message message = new Messages.C7_Message();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return SendMessageAsync(message);
                }

                public System.Threading.Tasks.Task TestCallback1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C8_Request message = new Messages.C8_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<Messages.C8_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<int> TestCallback2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C9_Request message = new Messages.C9_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<int, Messages.C9_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<string> TestCallback3(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C10_Request message = new Messages.C10_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return CallAsync<string, Messages.C10_Response>(message, cancelToken);
                }
            }

            public class TryFacade : SharpRpc.ClientFacadeBase
            {
                public TryFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public SharpRpc.RpcResult TestCallbackNotify1(int p1, string p2)
                {
                    Messages.C7_Message message = new Messages.C7_Message();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TrySendMessage(message);
                }

                public SharpRpc.RpcResult TestCallback1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C8_Request message = new Messages.C8_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<Messages.C8_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<int> TestCallback2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C9_Request message = new Messages.C9_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<int, Messages.C9_Response>(message, cancelToken).Result;
                }

                public SharpRpc.RpcResult<string> TestCallback3(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C10_Request message = new Messages.C10_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C10_Response>(message, cancelToken).Result;
                }
            }

            public class TryAsyncFacade : SharpRpc.ClientFacadeBase
            {
                public TryAsyncFacade(SharpRpc.Channel channel) : base(channel)
                {
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> TestCallbackNotify1(int p1, string p2)
                {
                    Messages.C7_Message message = new Messages.C7_Message();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TrySendMessageAsync(message);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult> TestCallback1(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C8_Request message = new Messages.C8_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<Messages.C8_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<int>> TestCallback2(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C9_Request message = new Messages.C9_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<int, Messages.C9_Response>(message, cancelToken);
                }

                public System.Threading.Tasks.Task<SharpRpc.RpcResult<string>> TestCallback3(int p1, string p2, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
                {
                    Messages.C10_Request message = new Messages.C10_Request();
                    message.Arg1 = p1;
                    message.Arg2 = p2;
                    return TryCallAsync<string, Messages.C10_Response>(message, cancelToken);
                }
            }
        }

        public abstract class CallbackServiceBase
        {
            public abstract System.Threading.Tasks.Task TestCallbackNotify1(int p1, string p2);
            public abstract System.Threading.Tasks.Task TestCallback1(SharpRpc.CallContext context, int p1, string p2);
            public abstract System.Threading.Tasks.Task<int> TestCallback2(SharpRpc.CallContext context, int p1, string p2);
            public abstract System.Threading.Tasks.Task<string> TestCallback3(SharpRpc.CallContext context, int p1, string p2);
            public virtual void OnResponseSent_TestCallback2(int responseValue)
            {
            }

            public virtual void OnResponseSent_TestCallback3(string responseValue)
            {
            }
        }

        private class CallbackServiceHandler : SharpRpc.RpcCallHandler
        {
            CallbackServiceBase _stub;
            public CallbackServiceHandler(CallbackServiceBase serviceImpl)
            {
                _stub = serviceImpl;
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCallback1(Messages.C8_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    await _stub.TestCallback1(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C8_Response();
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

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCallback2(Messages.C9_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.TestCallback2(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C9_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C9_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C9_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            private async System.Threading.Tasks.Task<SharpRpc.IResponseMessage> InvokeTestCallback3(Messages.C10_Request request)
            {
                var context = CreateCallContext(request);
                try
                {
                    var result = await _stub.TestCallback3(context, request.Arg1, request.Arg2);
                    CloseContext(context);
                    var response = new Messages.C10_Response();
                    response.Result = result;
                    return response;
                }
                catch (SharpRpc.RpcFaultException ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C10_Fault();
                    return OnRegularFault(faultMsg, ex.Message);
                }
                catch (System.Exception ex)
                {
                    CloseContext(context);
                    var faultMsg = new Messages.C10_Fault();
                    return OnUnexpectedFault(faultMsg, ex);
                }
            }

            protected override System.Threading.Tasks.Task OnMessage(SharpRpc.IMessage message)
            {
                if (message is Messages.C7_Message)
                {
                    var m0 = (Messages.C7_Message)message;
                    return _stub.TestCallbackNotify1(m0.Arg1, m0.Arg2);
                }
                else
                    return OnUnknownMessage(message);
            }

            protected override System.Threading.Tasks.Task<SharpRpc.IResponseMessage> OnRequest(SharpRpc.IRequestMessage request)
            {
                if (request is Messages.C10_Request)
                {
                    var r2 = (Messages.C10_Request)request;
                    return InvokeTestCallback3(r2);
                }
                else if (request is Messages.C9_Request)
                {
                    var r1 = (Messages.C9_Request)request;
                    return InvokeTestCallback2(r1);
                }
                else if (request is Messages.C8_Request)
                {
                    var r0 = (Messages.C8_Request)request;
                    return InvokeTestCallback1(r0);
                }
                else
                    return OnUnknownRequest(request);
            }

            protected override void OnResponseSent(SharpRpc.IResponseMessage response)
            {
                if (response is Messages.C10_Response)
                {
                    var r1 = (Messages.C10_Response)response;
                    _stub.OnResponseSent_TestCallback3(r1.Result);
                }
                else if (response is Messages.C9_Response)
                {
                    var r0 = (Messages.C9_Response)response;
                    _stub.OnResponseSent_TestCallback2(r0.Result);
                }
            }
        }
    }
}