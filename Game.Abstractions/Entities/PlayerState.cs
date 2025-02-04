namespace Game.Abstractions.Entities
{
    public class PlayerState
    {
        public bool Victory { get; set; }
        public DateTime EarliestDefeat { get; set; }
        public DateTime EnemyCooldown { get; set; }
    }
}
