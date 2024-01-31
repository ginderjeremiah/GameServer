using StackExchange.Redis;

namespace DataAccess.Redis
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class RedisSubscriberAttribute : Attribute
    {
        public RedisChannel Channel { get; set; }
        public RedisValue QueueName { get; set; }
        public RedisSubscriberAttribute(string channel, string queueName)
        {
            Channel = RedisChannel.Literal(channel);
            QueueName = queueName;
        }
    }
}
