using Game.Api.Models.Attributes;
using Game.Api.Models.Common;

namespace Game.Api.Models.Items
{
    public class AddEditItemAttributesData
    {
        public int ItemId { get; set; }
        public List<Change<BattlerAttribute>> Changes { get; set; }
    }
}
