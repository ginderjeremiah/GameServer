using GameInfrastructure.Cache;
using GameInfrastructure.Database;
using GameInfrastructure.Logging;
using GameInfrastructure.PubSub;

namespace GameInfrastructure
{
    public interface IDataServicesConfiguration : ILogConfiguration, IDatabaseConfiguration, ICacheConfiguration, IPubSubConfiguration
    {
    }
}
