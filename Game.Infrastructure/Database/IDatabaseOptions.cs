using Game.Core.Infrastructure;

namespace Game.Infrastructure.Database
{
    public interface IDatabaseOptions
    {
        public DatabaseSystem DatabaseSystem { get; }
        public string DbConnectionString { get; }
    }
}
