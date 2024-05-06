using DataAccess.Repositories;
using GameCore;
using GameCore.DataAccess;
using GameCore.Infrastructure;

namespace DataAccess
{
    public class RepositoryManager : IRepositoryManager
    {
        private readonly IDataServicesFactory _dataServices;
        private DataProviderSynchronizer? _synchronizer;
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

        private IDatabaseService Database => _dataServices.Database;
        private ICacheService Cache => _dataServices.Cache;
        private IPubSubService PubSub => _dataServices.PubSub;
        private DataProviderSynchronizer Synchronizer => _synchronizer ??= new(PubSub, this);

        public IInventoryItems InventoryItems => _inventoryItems ??= new InventoryItems(Database);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(Database, Cache, Synchronizer);
        public ILogPreferences LogPreferences => _logPreferences ??= new LogPreferences(Database);
        public IPlayers Players => _players ??= new Players(Database);
        public ITags Tags => _itemTags ??= new Tags(Database);
        public IItemMods ItemMods => _itemMods ??= new ItemMods(Database);
        public IItems Items => _items ??= new Items(Database);
        public IItemSlots ItemSlots => _itemSlots ??= new ItemSlots(Database);
        public IItemCategories ItemCategories => _itemCategories ??= new ItemCategories(Database);
        public ISlotTypes SlotTypes => _slotTypes ??= new SlotTypes(Database);
        public IEnemies Enemies => _enemies ??= new Enemies(Database);
        public IZones Zones => _zones ??= new Zones(Database);
        public ISkills Skills => _skills ??= new Skills(Database);
        public IAttributes Attributes => _attributes ??= new Attributes(Database);
        public IItemAttributes ItemAttributes => _itemAttributes ??= new ItemAttributes(Database);
        public ITagCategories TagCategories => _tagCategories ??= new TagCategories(Database);
        public IItemModAttributes ItemModAttributes => _itemModAttributes ??= new ItemModAttributes(Database);

        public RepositoryManager(IDataServicesFactory dataServices)
        {
            _dataServices = dataServices;
        }
    }
}


