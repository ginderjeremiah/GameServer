using DataAccess.Repositories;

namespace DataAccess
{
    public class RepositoryManager : IRepositoryManager
    {
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

        public IInventoryItems InventoryItems => _inventoryItems ??= new InventoryItems(_connectionString);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(_connectionString);
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

        public RepositoryManager(string connectionString)
        {
            _connectionString = connectionString;
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
    }
}


