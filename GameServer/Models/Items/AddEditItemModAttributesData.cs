using GameServer.Models.Attributes;
using GameServer.Models.Common;

namespace GameServer.Models.Items
{
    public class AddEditItemModAttributesData
    {
        public int ItemModId { get; set; }
        public List<Change<BattlerAttribute>> Changes { get; set; }
    }
}
