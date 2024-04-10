using DataAccess.Models.PlayerAttributes;
using static GameServer.AttributeType;

namespace GameServer
{
    public static class Extensions
    {
        public static bool IsCoreAttribute(this PlayerAttribute att)
        {
            return ((AttributeType)att.AttributeId).IsCoreAttribute();
        }

        public static bool IsCoreAttribute(this AttributeType att)
        {
            return att is Strength or Endurance or Intellect or Agility or Dexterity or Luck;
        }
    }
}