using DataAccess.Repositories;
using GameCore.DataAccess;
using GameCore.Infrastructure;
using GameInfrastructure;
using GameInfrastructure.Database;

namespace DataAccess
{
    public class RepositoryManager(IDataServicesFactory dataServices) : IRepositoryManager
    {
        private readonly IDataServicesFactory _dataServices = dataServices;
        private DataProviderSynchronizer? _synchronizer;
        private SessionStore? _sessionStore;
        private InventoryItems? _inventoryItems;
        private Players? _players;
        private Tags? _itemTags;
        private ItemMods? _itemMods;
        private Items? _items;
        private ItemCategories? _itemCategories;
        private SlotTypes? _slotTypes;
        private Enemies? _enemies;
        private Zones? _zones;
        private Skills? _skills;
        private Attributes? _attributes;
        private TagCategories? _tagCategories;

        private GameContext Database => _dataServices.DbContext;
        private ICacheService Cache => _dataServices.Cache;
        //private IPubSubService PubSub => _dataServices.PubSub;
        private DataProviderSynchronizer Synchronizer => _synchronizer ??= new(_dataServices);

        public IInventoryItems InventoryItems => _inventoryItems ??= new InventoryItems(Database);
        public ISessionStore SessionStore => _sessionStore ??= new SessionStore(Database, Cache);
        public IPlayers Players => _players ??= new Players(Database, Cache, Synchronizer);
        public ITags Tags => _itemTags ??= new Tags(Database);
        public IItemMods ItemMods => _itemMods ??= new ItemMods(Database);
        public IItems Items => _items ??= new Items(Database);
        public IItemCategories ItemCategories => _itemCategories ??= new ItemCategories(Database);
        public ISlotTypes SlotTypes => _slotTypes ??= new SlotTypes(Database);
        public IEnemies Enemies => _enemies ??= new Enemies(Database);
        public IZones Zones => _zones ??= new Zones(Database);
        public ISkills Skills => _skills ??= new Skills(Database);
        public IAttributes Attributes => _attributes ??= new Attributes(Database);
        public ITagCategories TagCategories => _tagCategories ??= new TagCategories(Database);

        public Task SaveChangesAsync()
        {
            return Database.SaveChangesAsync();
        }

        public void Insert<Entity>(Entity entity) where Entity : class
        {
            Database.Add(entity);
        }

        public void Delete<Entity>(Entity entity) where Entity : class
        {
            Database.Remove(entity);
        }

        public void Update<Entity>(Entity entity) where Entity : class
        {
            Database.Update(entity);
        }
    }
}


