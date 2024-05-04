using DataAccess;
using GameCore;
using GameServer.Models.Enemies;
using GameServer.Models.Player;

namespace GameServer.BattleSimulation
{
    public class BattleSimulator
    {
        private Mulberry32 Rng { get; set; }
        private BattlePlayer Player { get; set; }
        private BattleEnemy Enemy { get; set; }
        private const int msPerTick = 6;

        public BattleSimulator(PlayerData playerData, DataAccess.Entities.Enemies.Enemy enemy, EnemyInstance enemyInstance, IRepositoryManager repositories)
        {
            var skills = repositories.Skills.AllSkills();
            var items = repositories.Items.AllItems();
            var itemMods = repositories.ItemMods.AllItemMods();
            Rng = new Mulberry32(enemyInstance.Seed);
            Player = new BattlePlayer(playerData, skills, items, itemMods);
            Enemy = new BattleEnemy(enemy, enemyInstance, skills);
        }

        public bool Simulate(out int totalMs)
        {
            var maxMs = msPerTick * 10000;
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
