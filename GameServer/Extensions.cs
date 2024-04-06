using DataAccess.Models.PlayerAttributes;
using GameServer.Models.Attributes;

namespace GameServer
{
    public static class Extensions
    {
        public static bool IsCoreAttribute(this PlayerAttribute att)
        {
            return new BattlerAttribute(att).IsCoreAttribute;
        }
    }
}