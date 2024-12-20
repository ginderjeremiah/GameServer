﻿using Game.Api.Models.Attributes;
using Game.Api.Models.InventoryItems;
using Game.Core.Sessions;

namespace Game.Api.Models.Player
{
    public class PlayerData : IModel
    {
        public string UserName { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public List<int> SelectedSkills { get; set; }
        public int CurrentZone { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }
        public InventoryData InventoryData { get; set; }
        public List<LogPreference> LogPreferences { get; set; }

        public PlayerData() { }

        public PlayerData(SessionPlayer sessionPlayer, SessionInventory sessionInventory, int currentZone)
        {
            UserName = sessionPlayer.UserName;
            Name = sessionPlayer.Name;
            Level = sessionPlayer.Level;
            Exp = sessionPlayer.Exp;
            Attributes = sessionPlayer.Attributes.To().Model<BattlerAttribute>().ToList();
            SelectedSkills = sessionPlayer.SelectedSkills.Select(s => s.SkillId).ToList();
            StatPointsGained = sessionPlayer.StatPointsGained;
            StatPointsUsed = sessionPlayer.StatPointsUsed;
            InventoryData = new InventoryData(sessionInventory);
            LogPreferences = sessionPlayer.LogPreferences.To().Model<LogPreference>().ToList();
            CurrentZone = currentZone;
        }
    }
}
