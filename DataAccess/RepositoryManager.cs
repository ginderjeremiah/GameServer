using DataAccess.Repositories;
using GameCore.Cache;
using GameCore.Database.Interfaces;
using GameCore.Logging.Interfaces;
using GameCore.PubSub;

namespace DataAccess
{
    public class RepositoryManager : IRepositoryManager
    {
        private readonly IDataProvider _database;
        private readonly ICacheProvider _cache;
        private readonly DataProviderSynchronizer _synchronizer;
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
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(_database, _cache, _synchronizer);
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

        public RepositoryManager(IApiLogger logger, IDataProvider database, ICacheProvider cache, IPubSubProvider pubSubProvider)
        {
            _database = database;
            _cache = cache;
            _synchronizer = new(pubSubProvider, this);
        }
    }
}


