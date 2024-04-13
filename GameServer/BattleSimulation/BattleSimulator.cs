using DataAccess;
using GameLibrary;
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
            //Console.WriteLine("Starting Simulate.");
            //long startTime = Stopwatch.GetTimestamp();
            for (totalMs = msPerTick; totalMs <= msPerTick * 10000; totalMs += msPerTick)
            {
                foreach (var skill in Player.AdvancedCooldowns(msPerTick))
                {
                    Enemy.TakeDamage(skill.CalculateDamage(Player.Attributes));
                    //Console.WriteLine($"{totalMs}: Player uses {skill.Data.SkillName} and deals {skill.CalculateDamage(Player.Stats) - Enemy.DerivedStats.Defense} damage.");
                    //Console.WriteLine($"{totalMs}: Enemy hp: {Enemy.CurrentHealth}/{Enemy.DerivedStats.MaxHealth}");
                }
                if (Enemy.IsDead)
                {
                    //Console.WriteLine($"Finished Simulate: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}");
                    return true;
                }
                foreach (var skill in Enemy.AdvancedCooldowns(msPerTick))
                {
                    Player.TakeDamage(skill.CalculateDamage(Enemy.Attributes));
                    //Console.WriteLine($"{totalMs}: Enemy uses {skill.Data.SkillName} and deals {skill.CalculateDamage(Enemy.Stats) - Player.DerivedStats.Defense} damage.");
                    //Console.WriteLine($"{totalMs}: Player hp: {Player.CurrentHealth}/{Player.DerivedStats.MaxHealth}");
                }
                if (Player.IsDead)
                {
                    //Console.WriteLine($"Finished Simulate: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}");
                    return false;
                }
            }
            //Console.WriteLine($"Finished Simulate: {Stopwatch.GetElapsedTime(startTime).TotalMilliseconds}");
            return false;
        }
    }
}
