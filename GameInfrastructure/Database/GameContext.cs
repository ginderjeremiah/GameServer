using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Attribute = GameCore.Entities.Attribute;

namespace GameInfrastructure.Database
{
    public class GameContext(IDatabaseConfiguration config) : DbContext, IDatabaseService
    {
        private readonly IDatabaseConfiguration _config = config;

        public IQueryable<Attribute> Attributes => Set<Attribute>();
        public IQueryable<Enemy> Enemies => Set<Enemy>();
        public IQueryable<InventoryItem> InventoryItems => Set<InventoryItem>();
        public IQueryable<ItemAttribute> ItemAttributes => Set<ItemAttribute>();
        public IQueryable<ItemCategory> ItemCategories => Set<ItemCategory>();
        public IQueryable<ItemModAttribute> ItemModAttributes => Set<ItemModAttribute>();
        public IQueryable<ItemMod> ItemMods => Set<ItemMod>();
        public IQueryable<Item> Items => Set<Item>();
        public IQueryable<ItemSlot> ItemSlots => Set<ItemSlot>();
        public IQueryable<LogPreference> LogPreferences => Set<LogPreference>();
        public IQueryable<Player> Players => Set<Player>();
        public IQueryable<Skill> Skills => Set<Skill>();
        public IQueryable<SlotType> SlotTypes => Set<SlotType>();
        public IQueryable<TagCategory> TagCategories => Set<TagCategory>();
        public IQueryable<Tag> Tags => Set<Tag>();
        public IQueryable<ZoneEnemy> ZoneEnemies => Set<ZoneEnemy>();
        public IQueryable<ZoneEnemyAlias> ZoneEnemyAliases => Set<ZoneEnemyAlias>();
        public IQueryable<ZoneEnemyProbability> ZoneEnemyProbabilities => Set<ZoneEnemyProbability>();
        public IQueryable<Zone> Zones => Set<Zone>();

        public void Insert<Entity>(Entity entity) where Entity : class
        {
            var set = Set<Entity>();
            set.Add(entity);
        }

        public void Delete<Entity>(Entity entity) where Entity : class
        {
            var set = Set<Entity>();
            set.Remove(entity);
        }

        public new void Update<Entity>(Entity entity) where Entity : class
        {
            var set = Set<Entity>();
            set.Update(entity);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_config.DbConnectionString);
        }
    }
}
