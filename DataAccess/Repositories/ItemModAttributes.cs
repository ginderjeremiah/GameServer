using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class ItemModAttributes : BaseRepository, IItemModAttributes
    {
        public ItemModAttributes(string connectionString) : base(connectionString) { }

        public void AddItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            var commandText = @"
                INSERT INTO ItemModAttributes
                VALUES
                    (@ItemModId, @AttributeId, @Amount)";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemModId", itemModId),
                new SqlParameter("@AttributeId", attributeId),
                new SqlParameter("@Amount", amount)
            );
        }

        public void UpdateItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            var commandText = @"
                UPDATE ItemModAttributes
                SET Amount = @Amount
                WHERE ItemModId = @ItemModId
                AND AttributeId = @AttributeId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemModId", itemModId),
                new SqlParameter("@AttributeId", attributeId),
                new SqlParameter("@Amount", amount)
            );
        }

        public void DeleteItemModAttribute(int itemModId, int attributeId)
        {
            var commandText = @"
                DELETE ItemModAttributes
                WHERE ItemModId = @ItemModId
                AND AttributeId = @AttributeId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemModId", itemModId),
                new SqlParameter("@AttributeId", attributeId)
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
