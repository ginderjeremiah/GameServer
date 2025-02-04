﻿namespace Game.Abstractions.Entities
{
    public partial class ZoneEnemy
    {
        public int ZoneId { get; set; }
        public int EnemyId { get; set; }
        public int Weight { get; set; }

        public virtual Zone Zone { get; set; }
        public virtual Enemy Enemy { get; set; }
    }
}
