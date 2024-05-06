using GameCore;
using GameCore.DataAccess;
using GameCore.Infrastructure;
using GameTests.Mocks.DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess
{
    internal class MockRepositoryManager : IRepositoryManager
    {
        public ICacheService Cache { get; set; }
        public MockRepositoryManager(ICacheService cache)
        {
            Cache = cache;
            SessionStore = new MockSessionStore(Cache);
        }

        public IInventoryItems InventoryItems { get; set; } = new MockInventoryItems();

        public ISessionStore SessionStore { get; set; }

        public ILogPreferences LogPreferences { get; set; } = new MockLogPreferences();

        public IPlayers Players { get; set; } = new MockPlayers();

        public ITags Tags { get; set; } = new MockTags();

        public IItemMods ItemMods { get; set; } = new MockItemMods();

        public IItems Items { get; set; } = new MockItems();

        public IItemSlots ItemSlots { get; set; } = new MockItemSlots();

        public IItemCategories ItemCategories { get; set; } = new MockItemCategories();

        public ISlotTypes SlotTypes { get; set; } = new MockSlotTypes();

        public IEnemies Enemies { get; set; } = new MockEnemies();

        public IZones Zones { get; set; } = new MockZones();

        public ISkills Skills { get; set; } = new MockSkills();

        public IAttributes Attributes { get; set; } = new MockAttributes();

        public IItemAttributes ItemAttributes { get; set; } = new MockItemAttributes();

        public ITagCategories TagCategories { get; set; } = new MockTagCategories();

        public IItemModAttributes ItemModAttributes { get; set; } = new MockItemModAttributes();
    }
}
