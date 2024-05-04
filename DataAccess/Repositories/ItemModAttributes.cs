using GameCore.Database;
using GameCore.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class ItemModAttributes : BaseRepository, IItemModAttributes
    {
        public ItemModAttributes(IDataProvider database) : base(database) { }

        public void AddItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            var commandText = @"
                INSERT INTO ItemModAttributes
                VALUES
                    (@ItemModId, @AttributeId, @Amount)";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemModId", itemModId),
                new QueryParameter("@AttributeId", attributeId),
                new QueryParameter("@Amount", amount)
            );
        }

        public void UpdateItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            var commandText = @"
                UPDATE ItemModAttributes
                SET Amount = @Amount
                WHERE ItemModId = @ItemModId
                AND AttributeId = @AttributeId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemModId", itemModId),
                new QueryParameter("@AttributeId", attributeId),
                new QueryParameter("@Amount", amount)
            );
        }

        public void DeleteItemModAttribute(int itemModId, int attributeId)
        {
            var commandText = @"
                DELETE ItemModAttributes
                WHERE ItemModId = @ItemModId
                AND AttributeId = @AttributeId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemModId", itemModId),
                new QueryParameter("@AttributeId", attributeId)
            );
        }
    }

    public interface IItemModAttributes
    {
        public void AddItemModAttribute(int itemModId, int attributeId, decimal amount);
        public void UpdateItemModAttribute(int itemModId, int attributeId, decimal amount);
        public void DeleteItemModAttribute(int itemModId, int attributeId);
    }
}
