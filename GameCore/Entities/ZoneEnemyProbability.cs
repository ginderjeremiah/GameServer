﻿namespace GameCore.Entities
{
    public class ZoneEnemyProbability
    {
        public decimal Probability { get; set; }
        public int ZoneOrder { get; set; }
        public int ZoneEnemyId { get; set; }

        public virtual ZoneEnemy ZoneEnemy { get; set; }
    }
}
