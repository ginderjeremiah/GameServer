namespace Game.Application
{
    /// <summary>
    /// Carries the outcome of a successful enemy defeat.
    /// </summary>
    public class DefeatResult
    {
        public required int ExpReward { get; set; }
        public required int NewLevel { get; set; }
        public required int NewExp { get; set; }
        public required int StatPointsGained { get; set; }
        public required int StatPointsUsed { get; set; }
    }
}
