using GameServer.Models.Common;

namespace GameServer.Models.Response
{
    public class NewEnemy
    {
        public double? Cooldown { get; set; }
        public EnemyInstance? EnemyInstance { get; set; }
    }
}
