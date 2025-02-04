namespace Game.Abstractions.DataAccess
{
    public interface IRepositoryManager
    {
        public IAttributes Attributes { get; }
        public IEnemies Enemies { get; }
        public IInventories InventoryItems { get; }
        public IItemCategories ItemCategories { get; }
        public IItemMods ItemMods { get; }
        public IItemModTypes ItemModTypes { get; }
        public IItems Items { get; }
        public IPlayers Players { get; }
        public ISessionStore SessionStore { get; }
        public ISkills Skills { get; }
        public ITagCategories TagCategories { get; }
        public ITags Tags { get; }
        public IUsers Users { get; }
        public IZones Zones { get; }

        public Task SaveChangesAsync();
        public void Insert<Entity>(Entity entity) where Entity : class;
        public void InsertAll<Entity>(IEnumerable<Entity> entities) where Entity : class;
        public void Delete<Entity>(Entity entity) where Entity : class;
        public void Update<Entity>(Entity entity) where Entity : class;
    }
}
