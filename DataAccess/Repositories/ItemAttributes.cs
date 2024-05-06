using GameCore.DataAccess;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class ItemAttributes : BaseRepository, IItemAttributes
    {
        public ItemAttributes(IDatabaseService database) : base(database) { }

        public void AddItemAttribute(int itemId, int attributeId, decimal amount)
        {
            var commandText = @"
                INSERT INTO ItemAttributes
                VALUES
                    (@ItemId, @AttributeId, @Amount)";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemId", itemId),
                new QueryParameter("@AttributeId", attributeId),
                new QueryParameter("@Amount", amount)
            );
        }

        public void UpdateItemAttribute(int itemId, int attributeId, decimal amount)
        {
            var commandText = @"
                UPDATE ItemAttributes
                SET Amount = @Amount
                WHERE ItemId = @ItemId
                AND AttributeId = @AttributeId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemId", itemId),
                new QueryParameter("@AttributeId", attributeId),
                new QueryParameter("@Amount", amount)
            );
        }

        public void DeleteItemAttribute(int itemId, int attributeId)
        {
            var commandText = @"
                DELETE ItemAttributes
                WHERE ItemId = @ItemId
                AND AttributeId = @AttributeId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemId", itemId),
                new QueryParameter("@AttributeId", attributeId)
            );
        }
    }
}
