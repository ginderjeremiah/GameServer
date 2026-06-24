using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full enemy reference-data set over the socket.
    /// </summary>
    public class GetEnemies : AbstractReferenceDataCommand<Enemy>
    {
        private readonly IEnemies _enemies;

        public override string Name { get; set; } = nameof(GetEnemies);

        public GetEnemies(IEnemies enemies)
        {
            _enemies = enemies;
        }

        protected override IEnumerable<Enemy> GetReferenceData()
        {
            return _enemies.All();
        }

        protected override object VersionKey => _enemies.VersionKey;
    }
}
