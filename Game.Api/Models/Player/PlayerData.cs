using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using CorePlayer = Game.Core.Players.Player;

namespace Game.Api.Models.Player
{
    public class PlayerData : IModel
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public List<int> SelectedSkills { get; set; }
        public int CurrentZone { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }

        public static PlayerData FromPlayer(CorePlayer player)
        {
            var attributes = player.GetAttributes();
            return new PlayerData
            {
                Name = player.Name,
                Level = player.Level,
                Exp = player.Exp,
                CurrentZone = player.CurrentZoneId,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
                SelectedSkills = player.SelectedSkills.Select(s => s.Id).ToList(),
                Attributes = attributes.AllModifiers()
                    .GroupBy(m => m.Attribute)
                    .Select(g => new BattlerAttribute
                    {
                        AttributeId = g.Key,
                        Amount = (decimal)attributes[g.Key],
                    })
                    .ToList(),
            };
        }
    }
}
