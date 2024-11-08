﻿using Game.Core.Entities;
using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    public class BattleEnemy : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattleEnemy(Enemy enemy, EnemyInstance enemyInstance)
        {
            var rng = new Random();
            var selectedSkills = enemy.EnemySkills.OrderBy(es => rng.Next()).Take(4).Select(es => es.Skill).OrderBy(s => s.Id).ToList();
            var attributes = enemy.AttributeDistributions.Select(dist => new BattlerAttribute(dist, enemyInstance.Level)).ToList();
            Level = enemyInstance.Level;
            Attributes = new BattleAttributes(attributes);
            CurrentHealth = Attributes[MaxHealth];
            Skills = selectedSkills.Select(skill => new BattleSkill(skill)).ToList();
            enemyInstance.Attributes = attributes;
            enemyInstance.SelectedSkills = selectedSkills.Select(s => s.Id).ToList();
        }
    }
}
