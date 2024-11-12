using Game.Core.DataAccess;
using Game.Core.Infrastructure;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;

namespace Game.DataAccess
{
    internal class RepositoryManager(GameContext context, ICacheService cache, DataProviderSynchronizer synchronizer) : IRepositoryManager
    {
        private readonly GameContext _context = context;
        private readonly ICacheService _cache = cache;
        private readonly DataProviderSynchronizer _synchronizer = synchronizer;
        private SessionStore? _sessionStore;
        private InventoryItems? _inventoryItems;
        private Players? _players;
        private Tags? _itemTags;
        private ItemMods? _itemMods;
        private Items? _items;
        private ItemCategories? _itemCategories;
        private ItemModTypes? _itemModSlotTypes;
        private Enemies? _enemies;
        private Zones? _zones;
        private Skills? _skills;
        private Attributes? _attributes;
        private TagCategories? _tagCategories;

        public IInventoryItems InventoryItems => _inventoryItems ??= new InventoryItems(_context);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(_context, _cache);
        public IPlayers Players => _players ??= new Players(_context, _cache, _synchronizer);
        public ITags Tags => _itemTags ??= new Tags(_context);
        public IItemMods ItemMods => _itemMods ??= new ItemMods(_context);
        public IItems Items => _items ??= new Items(_context);
        public IItemCategories ItemCategories => _itemCategories ??= new ItemCategories(_context);
        public IItemModTypes ItemModTypes => _itemModSlotTypes ??= new ItemModTypes(_context);
        public IEnemies Enemies => _enemies ??= new Enemies(_context);
        public IZones Zones => _zones ??= new Zones(_context);
        public ISkills Skills => _skills ??= new Skills(_context);
        public IAttributes Attributes => _attributes ??= new Attributes(_context);
        public ITagCategories TagCategories => _tagCategories ??= new TagCategories(_context);

        public Task SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }

        public void Insert<Entity>(Entity entity) where Entity : class
        {
            _context.Add(entity);
        }

        public void Delete<Entity>(Entity entity) where Entity : class
        {
            _context.Remove(entity);
        }

        public void Update<Entity>(Entity entity) where Entity : class
        {
            _context.Update(entity);
        }
    }
}


