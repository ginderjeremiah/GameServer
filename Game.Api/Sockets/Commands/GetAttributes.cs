using Attribute = Game.Api.Models.Attributes.Attribute;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the intrinsic attribute reference-data collection. WebSocket
    /// equivalent of the <c>GET /api/Attributes</c> endpoint.
    /// </summary>
    public class GetAttributes : AbstractReferenceDataCommand<Attribute>
    {
        public override string Name { get; set; } = nameof(GetAttributes);

        protected override IEnumerable<Attribute> GetReferenceData()
        {
            return Core.Attributes.Attribute.GetAllAttributes().To().Model<Attribute>();
        }

        // Intrinsic (enum-derived) set: fixed for the process lifetime, so the version is memoized once.
        protected override object VersionKey => IntrinsicVersionKey<GetAttributes>.Instance;
    }
}
