using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class ItemAttributes : BaseRepository, IItemAttributes
    {
        public ItemAttributes(string connectionString) : base(connectionString) { }

        public void AddItemAttribute(int itemId, int attributeId, decimal amount)
        {
            var commandText = @"
                INSERT INTO ItemAttributes
                VALUES
                    (@ItemId, @AttributeId, @Amount)";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemId", itemId),
                new SqlParameter("@AttributeId", attributeId),
                new SqlParameter("@Amount", amount)
            );
        }

        public void UpdateItemAttribute(int itemId, int attributeId, decimal amount)
        {
            var commandText = @"
                UPDATE ItemAttributes
                SET Amount = @Amount
                WHERE ItemId = @ItemId
                AND AttributeId = @AttributeId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemId", itemId),
                new SqlParameter("@AttributeId", attributeId),
                new SqlParameter("@Amount", amount)
            );
        }

        public void DeleteItemAttribute(int itemId, int attributeId)
        {
            var commandText = @"
                DELETE ItemAttributes
                WHERE ItemId = @ItemId
                AND AttributeId = @AttributeId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemId", itemId),
                new SqlParameter("@AttributeId", attributeId)
            );
        }
    }

    public interface IItemAttributes
    {
        public void AddItemAttribute(int itemId, int attributeId, decimal amount);
        public void UpdateItemAttribute(int itemId, int attributeId, decimal amount);
        public void DeleteItemAttribute(int itemId, int attributeId);
    }
}
