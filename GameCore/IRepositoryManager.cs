using GameCore.DataAccess;

namespace GameCore
{
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
