using DataAccess.Caches;
using DataAccess.Models.Player;
using DataAccess.Models.SessionStore;
using DataAccess.Models.Skills;
using DataAccess.Models.Stats;
using GameServer.BattleSimulation;
using System.Data;
using System.Text.Json.Serialization;

namespace GameServer.Auth
{
    public class PlayerData
    {
        private readonly SessionData _sessionData;

        private Player Player => _sessionData.PlayerData;

        public int PlayerId { get => Player.PlayerId; }
        public string UserName { get => Player.UserName; }
        [JsonIgnore]
        public Guid Salt { get => Player.Salt; }
        [JsonIgnore]
        public string PassHash { get => Player.PassHash; }
        public string PlayerName { get => Player.PlayerName; }
        public int Level { get => Player.Level; set => Player.Level = value; }
        public int Exp { get => Player.Exp; set => Player.Exp = value; }
        public BattleBaseStats Stats { get; set; }
        public List<int> SelectedSkills { get => _sessionData.SelectedSkills; set => _sessionData.SelectedSkills = value; }
        public int StatPointsGained { get => Player.StatPointsGained; set => Player.StatPointsGained = value; }
        public int StatPointsUsed { get => Player.StatPointsUsed; private set => Player.StatPointsUsed = value; }

        public PlayerData(SessionData sessionData)
        {
            _sessionData = sessionData;
            Stats = new BattleBaseStats()
            {
                Strength = sessionData.Stats.Strength,
                Endurance = sessionData.Stats.Endurance,
                Intellect = sessionData.Stats.Intellect,
                Agility = sessionData.Stats.Agility,
                Dexterity = sessionData.Stats.Dexterity,
                Luck = sessionData.Stats.Luck,
            };
        }

        public List<Skill> GetSkills(ISkillCache skillCache)
        {
            return SelectedSkills.Select(skillCache.GetSkill).ToList();
        }

        public bool ChangeStats(BattleBaseStats changedStats)
        {
            var changed = changedStats.Total + Stats.Total <= StatPointsGained && Stats.ChangeStats(changedStats);
            if (changed)
            {
                StatPointsUsed = Stats.Total;
                _sessionData.Stats = new BaseStats()
                {
                    Strength = Stats.Strength,
                    Endurance = Stats.Endurance,
                    Intellect = Stats.Intellect,
                    Agility = Stats.Agility,
                    Dexterity = Stats.Dexterity,
                    Luck = Stats.Luck
                };
            }
            return changed;
        }
    }
}
