namespace Game.Infrastructure.Entities
{
    /// <summary>Join row linking an <see cref="ItemMod"/> to a <see cref="Tag"/>. The item-mod counterpart of
    /// <see cref="ItemTag"/>; see it for why the join is modeled explicitly.</summary>
    public class ItemModTag
    {
        public int ItemModId { get; set; }
        public int TagId { get; set; }
    }
}
