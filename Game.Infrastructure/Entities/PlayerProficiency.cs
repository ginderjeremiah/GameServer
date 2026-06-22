namespace Game.Infrastructure.Entities
{
    public class PlayerProficiency
    {
        public int PlayerId { get; set; }
        public int ProficiencyId { get; set; }
        public int Level { get; set; }
        public decimal Xp { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual Proficiency Proficiency { get => field ?? throw new NotLoadedException(nameof(Proficiency)); set; }
    }
}
