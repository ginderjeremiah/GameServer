using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class SlotTypes : BaseRepository, ISlotTypes
    {
        public SlotTypes(IDatabaseService database) : base(database) { }

        public IQueryable<SlotType> AllSlotTypes()
        {
            return Database.SlotTypes;
        }
    }
}
