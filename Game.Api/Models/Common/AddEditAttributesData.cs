using Game.Abstractions.Contracts;

namespace Game.Api.Models.Common
{
    public class AddEditAttributesData
    {
        public int Id { get; set; }
        public required List<Change<BattlerAttribute>> Changes { get; set; }
    }
}
