﻿namespace Game.Core.Entities
{
    public partial class ZoneEnemyAlias
    {
        public int ZoneEnemyId { get; set; }
        public int AliasZoneEnemyId { get; set; }

        public virtual ZoneEnemy ZoneEnemy { get; set; }
    }
}
