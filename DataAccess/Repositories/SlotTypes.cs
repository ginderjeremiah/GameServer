using DataAccess.Entities.SlotTypes;

namespace DataAccess.Repositories
{
    internal class SlotTypes : BaseRepository, ISlotTypes
    {
        public SlotTypes(string connectionString) : base(connectionString) { }

        public List<SlotType> AllSlotTypes()
        {
            var commandText = @"
                SELECT
                    SlotTypeId,
                    SlotTypeName
                FROM
                    SlotTypes";

            return QueryToList<SlotType>(commandText);
        }
    }

    public interface ISlotTypes
    {
        public List<SlotType> AllSlotTypes();
    }
}
