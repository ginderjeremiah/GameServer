using Game.Core.Entities;
using Game.Core.Sessions;

namespace Game.Core.BattleSimulation
{
    public class BattleSimulator
    {
        private Mulberry32 Rng { get; set; }
        private BattlePlayer Player { get; set; }
        private BattleEnemy Enemy { get; set; }

        private const int msPerTick = 40;
        private const int maxMs = msPerTick * 10000;

        public BattleSimulator(Session session, EnemyInstance enemyInstance, IEnumerable<Skill> enemySkills)
        {
            Rng = new Mulberry32(enemyInstance.Seed);
            Player = new BattlePlayer(session);
            Enemy = new BattleEnemy(enemyInstance, enemySkills);
        }

        public bool Simulate(out int totalMs)
        {
            for (totalMs = msPerTick; totalMs <= maxMs; totalMs += msPerTick)
            {
                foreach (var skill in Player.AdvancedCooldowns(msPerTick))
                {
                    Enemy.TakeDamage(skill.CalculateDamage(Player.Attributes));
                }

                if (Enemy.IsDead)
                {
                    return true;
                }

                foreach (var skill in Enemy.AdvancedCooldowns(msPerTick))
                {
                    Player.TakeDamage(skill.CalculateDamage(Enemy.Attributes));
                }

                if (Player.IsDead)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
