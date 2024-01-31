using StackExchange.Redis;

namespace DataAccess.Redis
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class RedisSubscriberAttribute : Attribute
    {
        public RedisChannel Channel { get; set; }
        public RedisSubscriberAttribute(string channel)
        {
            Channel = RedisChannel.Literal(channel);
        }
    }
}
