using GameCore.Entities;
using Attribute = GameCore.Entities.Attribute;

namespace GameCore.Infrastructure
{
    public interface IDatabaseService
    {
        public IQueryable<Attribute> Attributes { get; }
        public IQueryable<Enemy> Enemies { get; }
        public IQueryable<InventoryItem> InventoryItems { get; }
        public IQueryable<ItemAttribute> ItemAttributes { get; }
        public IQueryable<ItemCategory> ItemCategories { get; }
        public IQueryable<ItemModAttribute> ItemModAttributes { get; }
        public IQueryable<ItemMod> ItemMods { get; }
        public IQueryable<Item> Items { get; }
        public IQueryable<ItemSlot> ItemSlots { get; }
        public IQueryable<LogPreference> LogPreferences { get; }
        public IQueryable<Player> Players { get; }
        public IQueryable<Skill> Skills { get; }
        public IQueryable<SlotType> SlotTypes { get; }
        public IQueryable<TagCategory> TagCategories { get; }
        public IQueryable<Tag> Tags { get; }
        public IQueryable<ZoneEnemy> ZoneEnemies { get; }
        public IQueryable<ZoneEnemyProbability> ZoneEnemyProbabilities { get; }
        public IQueryable<ZoneEnemyAlias> ZoneEnemyAliases { get; }
        public IQueryable<Zone> Zones { get; }

        public Task SaveChangesAsync();
        public void Insert<Entity>(Entity entity) where Entity : class;
        public void Delete<Entity>(Entity entity) where Entity : class;
        public void Update<Entity>(Entity entity) where Entity : class;
        public void Untrack<Entity>(Entity entity) where Entity : class;
        public Task EnsureDbUpdatedAsync();
    }
}
