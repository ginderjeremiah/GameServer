namespace GameCore.DataAccess
{
    public interface IItemAttributes
    {
        public void AddItemAttribute(int itemId, int attributeId, decimal amount);
        public void UpdateItemAttribute(int itemId, int attributeId, decimal amount);
        public void DeleteItemAttribute(int itemId, int attributeId);
    }
}
