using CorePlayerProficiency = Game.Core.Progress.PlayerProficiency;

namespace Game.Api.Models.Progress
{
    public class PlayerProficiency : IModelFromSource<PlayerProficiency, CorePlayerProficiency>
    {
        public int ProficiencyId { get; set; }
        public int Level { get; set; }
        public decimal Xp { get; set; }

        public static PlayerProficiency FromSource(CorePlayerProficiency source)
        {
            return new PlayerProficiency
            {
                ProficiencyId = source.ProficiencyId,
                Level = source.Level,
                Xp = source.Xp,
            };
        }
    }
}
