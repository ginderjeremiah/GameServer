using DataAccess.Entities.SlotTypes;
using GameCore.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class SlotTypes : BaseRepository, ISlotTypes
    {
        public SlotTypes(IDataProvider database) : base(database) { }

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

    public interface ISlotTypes
    {
        public List<SlotType> AllSlotTypes();
    }
}
