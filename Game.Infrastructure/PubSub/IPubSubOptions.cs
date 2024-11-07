using Game.Core.Infrastructure;

namespace Game.Infrastructure.PubSub
{
    public interface IPubSubOptions
    {
        public PubSubSystem PubSubSystem { get; }
        public string PubSubConnectionString { get; }
    }
}
