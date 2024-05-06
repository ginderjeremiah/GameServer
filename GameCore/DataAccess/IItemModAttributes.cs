namespace GameCore.DataAccess
{
    public interface IItemModAttributes
    {
        public void AddItemModAttribute(int itemModId, int attributeId, decimal amount);
        public void UpdateItemModAttribute(int itemModId, int attributeId, decimal amount);
        public void DeleteItemModAttribute(int itemModId, int attributeId);
    }
}
