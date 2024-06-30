using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Attribute = GameCore.Entities.Attribute;

namespace GameInfrastructure.Database
{
    internal class DatabaseService : IDatabaseService
    {
        private readonly GameContext _context;

        public DatabaseService(IDatabaseConfiguration config)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameContext>().UseSqlServer(config.DbConnectionString);
            _context = new(optionsBuilder.Options);
        }

        public IQueryable<Attribute> Attributes => _context.Attributes;
        public IQueryable<Enemy> Enemies => _context.Enemies;
        public IQueryable<InventoryItem> InventoryItems => _context.InventoryItems;
        public IQueryable<ItemAttribute> ItemAttributes => _context.ItemAttributes;
        public IQueryable<ItemCategory> ItemCategories => _context.ItemCategories;
        public IQueryable<ItemModAttribute> ItemModAttributes => _context.ItemModAttributes;
        public IQueryable<ItemMod> ItemMods => _context.ItemMods;
        public IQueryable<Item> Items => _context.Items;
        public IQueryable<ItemSlot> ItemSlots => _context.ItemSlots;
        public IQueryable<LogPreference> LogPreferences => _context.LogPreferences;
        public IQueryable<Player> Players => _context.Players;
        public IQueryable<Skill> Skills => _context.Skills;
        public IQueryable<SlotType> SlotTypes => _context.SlotTypes;
        public IQueryable<TagCategory> TagCategories => _context.TagCategories;
        public IQueryable<Tag> Tags => _context.Tags;
        public IQueryable<ZoneEnemy> ZoneEnemies => _context.ZoneEnemies;
        public IQueryable<ZoneEnemyAlias> ZoneEnemyAliases => _context.ZoneEnemyAliases;
        public IQueryable<ZoneEnemyProbability> ZoneEnemyProbabilities => _context.ZoneEnemyProbabilities;
        public IQueryable<Zone> Zones => _context.Zones;

        public void Insert<Entity>(Entity entity) where Entity : class
        {
            var set = _context.Set<Entity>();
            set.Add(entity);
        }

        public void Delete<Entity>(Entity entity) where Entity : class
        {
            var set = _context.Set<Entity>();
            set.Remove(entity);
        }

        public void Update<Entity>(Entity entity) where Entity : class
        {
            var set = _context.Set<Entity>();
            set.Update(entity);
        }

        public void Untrack<Entity>(Entity entity) where Entity : class
        {
            _context.Entry(entity).State = EntityState.Detached;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task EnsureDbUpdatedAsync()
        {
            await _context.Database.MigrateAsync();
        }
    }
}
