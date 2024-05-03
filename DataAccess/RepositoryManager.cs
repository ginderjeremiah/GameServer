using DataAccess.Redis;
using DataAccess.Repositories;
using GameLibrary.Database.Interfaces;
using GameLibrary.Logging;

namespace DataAccess
{
    public class RepositoryManager : IRepositoryManager
    {
        private readonly IDataProvider _database;
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

        public IInventoryItems InventoryItems => _inventoryItems ??= new InventoryItems(_database);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(_database, Redis);
        public ILogPreferences LogPreferences => _logPreferences ??= new LogPreferences(_database);
        public IPlayers Players => _players ??= new Players(_database);
        public ITags Tags => _itemTags ??= new Tags(_database);
        public IItemMods ItemMods => _itemMods ??= new ItemMods(_database);
        public IItems Items => _items ??= new Items(_database);
        public IItemSlots ItemSlots => _itemSlots ??= new ItemSlots(_database);
        public IItemCategories ItemCategories => _itemCategories ??= new ItemCategories(_database);
        public ISlotTypes SlotTypes => _slotTypes ??= new SlotTypes(_database);
        public IEnemies Enemies => _enemies ??= new Enemies(_database);
        public IZones Zones => _zones ??= new Zones(_database);
        public ISkills Skills => _skills ??= new Skills(_database);
        public IAttributes Attributes => _attributes ??= new Attributes(_database);
        public IItemAttributes ItemAttributes => _itemAttributes ??= new ItemAttributes(_database);
        public ITagCategories TagCategories => _tagCategories ??= new TagCategories(_database);
        public IItemModAttributes ItemModAttributes => _itemModAttributes ??= new ItemModAttributes(_database);
        internal RedisStore Redis { get; }

        public RepositoryManager(IDataConfiguration config, IApiLogger logger, IDataProvider database)
        {
            _database = database;
            Redis = RedisStore.GetInstance(config, logger, database);
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


