using MessagePack;
using SharpRpc;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Common
{
    public class BenchmarkClient : ClientBase
    {
        public BenchmarkClient(ClientEndpoint endpoint) : base(CreateEndpoint())
        {
        }

        public Task Connect()
        {
            return Channel.ConnectAsync();
        }

        public bool OnUpdate(FooEntity entity)
        {
            return Channel.TrySend(new EntityMessage<FooEntity>(entity));
        }

        public ValueTask<bool> OnUpdateAsync(FooEntity entity)
        {
            return Channel.TrySendAsync(new EntityMessage<FooEntity>(entity));
        }

        private static ClientEndpoint CreateEndpoint()
        {
            var endpoint = new TcpClientEndpoint("localhost", 812);
            endpoint.Serializer = new EntityMessageSerializer();
            return endpoint;
        }
    }


    [MessagePackObject]
    [Union(10, typeof(EntityMessage<FooEntity>))]
    public abstract class Message : IMessage
    {
    }

    [MessagePackObject]
    public class EntityMessage<T> : Message
    {
        public EntityMessage()
        {
        }

        public EntityMessage(T entity)
        {
            Entity = entity;
        }

        [Key(1)]
        public T Entity { get; set; }

        //public void Serialize(MessageWriter writer)
        //{
        //    MessagePack.MessagePackSerializer.Serialize<T>(writer.ByteBuffer, Entity);
        //}
    }

    public class EntityMessageSerializer : IRpcSerializer
    {
        public IMessage Deserialize(MessageReader reader)
        {
            return MessagePack.MessagePackSerializer.Deserialize<Message>(reader.ByteBuffer);
        }

        public void Serialize(IMessage message, MessageWriter writer)
        {
            MessagePack.MessagePackSerializer.Serialize<Message>(writer.ByteBuffer, (Message)message);
        }
    }
}
