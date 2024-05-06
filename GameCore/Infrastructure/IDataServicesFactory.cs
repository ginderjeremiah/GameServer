namespace GameCore.Infrastructure
{
    public interface IDataServicesFactory
    {
        public IApiLogger Logger { get; }
        public IDatabaseService Database { get; }
        public ICacheService Cache { get; }
        public IPubSubService PubSub { get; }
    }
}
