namespace Game.Infrastructure.Entities
{
    /// <summary>Join row linking an <see cref="Item"/> to a <see cref="Tag"/>. Modeled explicitly (rather than
    /// as an implicit many-to-many table) so the admin tag-setting path can add or remove a single
    /// assignment by inserting/deleting one navigation-free join row, without loading a tag's full
    /// item membership.</summary>
    public class ItemTag
    {
        public int ItemId { get; set; }
        public int TagId { get; set; }
    }
}
