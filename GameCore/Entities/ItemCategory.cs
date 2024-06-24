namespace GameCore.Entities
{
    public class ItemCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<Item> Items { get; set; }
    }
}