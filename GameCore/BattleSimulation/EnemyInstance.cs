namespace GameCore.BattleSimulation
{
    public class EnemyInstance
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }
        public List<int> SelectedSkills { get; set; }

        public string Hash()
        {
            var data = $"{Id}{Level}{string.Join(",", Attributes.Select(att => (double)att.Amount))}";
            return data.Hash(Seed.ToString(), 1);
        }
    }
}
