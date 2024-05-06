using GameCore.DataAccess;
using GameCore.Entities.ItemMods;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemModAttributes : IItemModAttributes
    {
        public List<ItemModAttribute> ItemModAttributes { get; set; } = new();
        public void AddItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            ItemModAttributes.Add(new ItemModAttribute { Amount = amount, AttributeId = attributeId, ItemModId = itemModId });
        }

        public void DeleteItemModAttribute(int itemModId, int attributeId)
        {
            var attToRemove = ItemModAttributes.FirstOrDefault(att => att.ItemModId == itemModId && att.AttributeId == attributeId);
            if (attToRemove != null)
            {
                ItemModAttributes.Remove(attToRemove);
            }
        }

        public void UpdateItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            var attToUpdate = ItemModAttributes.FirstOrDefault(att => att.ItemModId == itemModId && att.AttributeId == attributeId);
            if (attToUpdate != null)
            {
                attToUpdate.Amount = amount;
            }
        }
    }
}
