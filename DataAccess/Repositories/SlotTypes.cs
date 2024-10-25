using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;

namespace DataAccess.Repositories
{
    internal class SlotTypes : BaseRepository, ISlotTypes
    {
        public SlotTypes(GameContext database) : base(database) { }

        public IQueryable<ItemModSlotType> AllItemModSlotTypes()
        {
            return Database.SlotTypes;
        }
    }
}
