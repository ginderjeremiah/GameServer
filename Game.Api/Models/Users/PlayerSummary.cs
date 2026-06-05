using PlayerEntity = Game.Abstractions.Entities.Player;

namespace Game.Api.Models.Users
{
    /// <summary>
    /// A lightweight admin view of one of a user's players.
    /// </summary>
    public class PlayerSummary : IModelFromSource<PlayerSummary, PlayerEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int Level { get; set; }
        public DateTime LastActivity { get; set; }

        public static PlayerSummary FromSource(PlayerEntity player)
        {
            return new PlayerSummary
            {
                Id = player.Id,
                Name = player.Name,
                Level = player.Level,
                LastActivity = player.LastActivity,
            };
        }
    }
}
