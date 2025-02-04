using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Abstractions.Infrastructure;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess
{
    internal class RepositoryManager(GameContext context, ICacheService cache, DataProviderSynchronizer synchronizer) : IRepositoryManager
    {
        private readonly GameContext _context = context;
        private readonly ICacheService _cache = cache;
        private readonly DataProviderSynchronizer _synchronizer = synchronizer;

        private Attributes? _attributes;
        private Enemies? _enemies;
        private Inventories? _inventoryItems;
        private ItemCategories? _itemCategories;
        private ItemMods? _itemMods;
        private ItemModTypes? _itemModSlotTypes;
        private Items? _items;
        private Players? _players;
        private SessionStore? _sessionStore;
        private Skills? _skills;
        private TagCategories? _tagCategories;
        private Tags? _itemTags;
        private Users? _users;
        private Zones? _zones;

        public IAttributes Attributes => _attributes ??= new Attributes(_context);
        public IEnemies Enemies => _enemies ??= new Enemies(_context);
        public IInventories InventoryItems => _inventoryItems ??= new InventoryItems(_context);
        public IItemCategories ItemCategories => _itemCategories ??= new ItemCategories(_context);
        public IItemMods ItemMods => _itemMods ??= new ItemMods(_context);
        public IItemModTypes ItemModTypes => _itemModSlotTypes ??= new ItemModTypes(_context);
        public IItems Items => _items ??= new Items(_context);
        public IPlayers Players => _players ??= new Players(_context, _cache, _synchronizer);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(_context, _cache);
        public ISkills Skills => _skills ??= new Skills(_context);
        public ITagCategories TagCategories => _tagCategories ??= new TagCategories(_context);
        public ITags Tags => _itemTags ??= new Tags(_context);
        public IUsers Users => _users ??= new Users(_context);
        public IZones Zones => _zones ??= new Zones(_context);

        public Task SaveChangesAsync()
        {
            foreach (var entry in _context.ChangeTracker.Entries())
            {
                var tempProps = entry.Properties.Where(p => p.IsTemporary);
                if (entry.State is not EntityState.Added)
                {
                    var idProp = tempProps.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (idProp is not null && entry.Entity is IZeroBasedIdentityEntity zeroBasedIdentityEntity && zeroBasedIdentityEntity.Id == 0)
                    {
                        idProp.IsTemporary = false;
                        idProp.CurrentValue = 0;
                    }
                }

                var foreignKeyProps = tempProps.Where(p => p.Metadata.IsForeignKey() && p.Metadata.ClrType == typeof(int));
                foreach (var foreignKeyProp in foreignKeyProps)
                {
                    foreignKeyProp.IsTemporary = false;
                    foreignKeyProp.CurrentValue = 0;
                }
            }

            return _context.SaveChangesAsync();
        }

        public void Insert<Entity>(Entity entity) where Entity : class
        {
            _context.Add(entity);
        }

        public void InsertAll<Entity>(IEnumerable<Entity> entities) where Entity : class
        {
            _context.AddRange(entities);
        }

        public void Delete<Entity>(Entity entity) where Entity : class
        {
            var entry = _context.Entry(entity);
            if (entry.State is EntityState.Detached)
            {
                entry.State = EntityState.Unchanged;
            }

            _context.Remove(entity);
        }

        public void Update<Entity>(Entity entity) where Entity : class
        {
            var entry = _context.Update(entity);
            if (entry.State is not EntityState.Added)
            {
                var idProp = entry.Properties.FirstOrDefault(p => p.IsTemporary && p.Metadata.IsPrimaryKey());
                if (idProp is not null)
                {
                    idProp.IsTemporary = false;
                    idProp.CurrentValue = 0;
                    entry.State = EntityState.Modified;
                }
            }
        }
    }
}


