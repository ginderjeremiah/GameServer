namespace Game.Api.Models.Enemies
{
    public class ChallengeBossRequest
    {
        /// <summary>The zone whose dedicated boss to challenge. Defaults to the player's current zone when
        /// omitted.</summary>
        public int? ZoneId { get; set; }
    }
}
