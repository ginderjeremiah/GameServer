namespace GameCore.DataAccess
{
    public interface IRepositoryManager
    {
        public IInventoryItems InventoryItems { get; }
        public ISessionStore SessionStore { get; }
        public IPlayers Players { get; }
        public ITags Tags { get; }
        public IItemMods ItemMods { get; }
        public IItems Items { get; }
        public IItemCategories ItemCategories { get; }
        public ISlotTypes SlotTypes { get; }
        public IEnemies Enemies { get; }
        public IZones Zones { get; }
        public ISkills Skills { get; }
        public IAttributes Attributes { get; }
        public ITagCategories TagCategories { get; }

        public Task SaveChangesAsync();
        public void Insert<Entity>(Entity entity) where Entity : class;
        public void Delete<Entity>(Entity entity) where Entity : class;
        public void Update<Entity>(Entity entity) where Entity : class;
    }
}
