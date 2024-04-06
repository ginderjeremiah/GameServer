using static DataAccess.Attributes;
namespace DataAccess.Models.PlayerAttributes
{
    public class PlayerAttribute
    {
        public int PlayerId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }
        public bool IsCoreAttribute => (DataAccess.Attributes)AttributeId is Strength or Endurance or Intellect or Agility or Dexterity or Luck;
    }
}
