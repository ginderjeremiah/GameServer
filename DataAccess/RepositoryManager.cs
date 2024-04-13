using DataAccess.Redis;
using DataAccess.Repositories;
using GameLibrary.Logging;

namespace DataAccess
{
    public class RepositoryManager : IRepositoryManager
    {
        private readonly IApiLogger _logger;
        private readonly string _connectionString;
        private SessionStore? _sessionStore;
        private InventoryItems? _inventoryItems;
        private LogPreferences? _logPreferences;
        private Players? _players;
        private Tags? _itemTags;
        private ItemMods? _itemMods;
        private Items? _items;
        private ItemSlots? _itemSlots;
        private ItemCategories? _itemCategories;
        private SlotTypes? _slotTypes;
        private Enemies? _enemies;
        private Zones? _zones;
        private Skills? _skills;
        private Attributes? _attributes;
        private ItemAttributes? _itemAttributes;
        private TagCategories? _tagCategories;
        private ItemModAttributes? _itemModAttributes;

        public IInventoryItems InventoryItems => _inventoryItems ??= new InventoryItems(_connectionString, this);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(_connectionString, Redis);
        public ILogPreferences LogPreferences => _logPreferences ??= new LogPreferences(_connectionString);
        public IPlayers Players => _players ??= new Players(_connectionString);
        public ITags Tags => _itemTags ??= new Tags(_connectionString);
        public IItemMods ItemMods => _itemMods ??= new ItemMods(_connectionString);
        public IItems Items => _items ??= new Items(_connectionString);
        public IItemSlots ItemSlots => _itemSlots ??= new ItemSlots(_connectionString);
        public IItemCategories ItemCategories => _itemCategories ??= new ItemCategories(_connectionString);
        public ISlotTypes SlotTypes => _slotTypes ??= new SlotTypes(_connectionString);
        public IEnemies Enemies => _enemies ??= new Enemies(_connectionString);
        public IZones Zones => _zones ??= new Zones(_connectionString);
        public ISkills Skills => _skills ??= new Skills(_connectionString);
        public IAttributes Attributes => _attributes ??= new Attributes(_connectionString);
        public IItemAttributes ItemAttributes => _itemAttributes ??= new ItemAttributes(_connectionString);
        public ITagCategories TagCategories => _tagCategories ??= new TagCategories(_connectionString);
        public IItemModAttributes ItemModAttributes => _itemModAttributes ??= new ItemModAttributes(_connectionString);
        internal RedisStore Redis { get; }

        public RepositoryManager(IDataConfiguration config, IApiLogger logger)
        {
            _connectionString = config.DbConnectionString;
            _logger = logger;
            Redis = RedisStore.GetInstance(config, logger);
        }
    }

    public interface IRepositoryManager
    {
        public IInventoryItems InventoryItems { get; }
        public ISessionStore SessionStore { get; }
        public ILogPreferences LogPreferences { get; }
        public IPlayers Players { get; }
        public ITags Tags { get; }
        public IItemMods ItemMods { get; }
        public IItems Items { get; }
        public IItemSlots ItemSlots { get; }
        public IItemCategories ItemCategories { get; }
        public ISlotTypes SlotTypes { get; }
        public IEnemies Enemies { get; }
        public IZones Zones { get; }
        public ISkills Skills { get; }
        public IAttributes Attributes { get; }
        public IItemAttributes ItemAttributes { get; }
        public ITagCategories TagCategories { get; }
        public IItemModAttributes ItemModAttributes { get; }
    }
}


