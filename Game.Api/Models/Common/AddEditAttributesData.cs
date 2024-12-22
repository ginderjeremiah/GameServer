using Game.Api.Models.Attributes;

namespace Game.Api.Models.Common
{
    public class AddEditAttributesData
    {
        public int Id { get; set; }
        public List<Change<BattlerAttribute>> Changes { get; set; }
    }
}
