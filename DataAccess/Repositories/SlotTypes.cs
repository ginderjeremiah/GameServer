using GameCore.DataAccess;
using GameCore.Entities.SlotTypes;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class SlotTypes : BaseRepository, ISlotTypes
    {
        public SlotTypes(IDatabaseService database) : base(database) { }

        public List<SlotType> AllSlotTypes()
        {
            var commandText = @"
                SELECT
                    SlotTypeId,
                    SlotTypeName
                FROM
                    SlotTypes";

            return Database.QueryToList<SlotType>(commandText);
        }
    }
}
