namespace Game.Abstractions.Contracts
{
    /// <summary>A starter skill of a creatable class, with its name resolved server-side so the
    /// create-character screen can preview the kit before reference data is available (it runs
    /// pre-player-selection, where the socket — and the skills catalogue it serves — is not).</summary>
    public class CreatableClassSkill : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
