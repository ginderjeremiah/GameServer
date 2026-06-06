using Game.Abstractions.DataAccess;
using Game.Api.Models.Enemies;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full enemy reference-data collection. WebSocket equivalent of
    /// the <c>GET /api/Enemies</c> endpoint.
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
            return _enemies.All().To().Model<Enemy>();
        }
    }
}
