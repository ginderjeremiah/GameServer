using GameCore.DataAccess;
using GameCore.Entities.Items;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemAttributes : IItemAttributes
    {
        public List<ItemAttribute> ItemAttributes { get; set; } = new();
        public void AddItemAttribute(int itemId, int attributeId, decimal amount)
        {
            ItemAttributes.Add(new ItemAttribute { Amount = amount, AttributeId = attributeId, ItemId = itemId });
        }

        public void DeleteItemAttribute(int itemId, int attributeId)
        {
            var attToRemove = ItemAttributes.FirstOrDefault(att => att.ItemId == itemId && att.AttributeId == attributeId);
            if (attToRemove != null)
            {
                ItemAttributes.Remove(attToRemove);
            }
        }

        public void UpdateItemAttribute(int itemId, int attributeId, decimal amount)
        {
            var attToUpdate = ItemAttributes.FirstOrDefault(att => att.ItemId == itemId && att.AttributeId == attributeId);
            if (attToUpdate != null)
            {
                attToUpdate.Amount = amount;
            }
        }
    }
}
