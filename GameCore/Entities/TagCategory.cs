namespace GameCore.Entities
{
    public class TagCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<Tag> Tags { get; set; }
    }
}
