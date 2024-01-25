using GameServer.Models.Common;

namespace GameServer.Models.Response
{
    public class NewEnemyResponse
    {
        public double? Cooldown { get; set; }
        public EnemyInstance? EnemyInstance { get; set; }
    }
}
