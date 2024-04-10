using GameServer.Models.Attributes;
using GameServer.Models.Common;

namespace GameServer.Models.Items
{
    public class AddEditItemAttributesData
    {
        public int ItemId { get; set; }
        public List<Change<BattlerAttribute>> Changes { get; set; }
    }
}
