using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure.Database;

namespace GameInfrastructure
{
    public interface IDataServicesFactory
    {
        public IApiLogger Logger { get; }
        public GameContext DbContext { get; }
        public ICacheService Cache { get; }
        public IPubSubService PubSub { get; }

        public GameContext GetNewDbContext();
    }
}
