using Game.Abstractions.Contracts;

namespace Game.Api.Models.Enemies
{
    public class EnemyInstance : IModel
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }
        public required List<int> SelectedSkills { get; set; }
    }
}
