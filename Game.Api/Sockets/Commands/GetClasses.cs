using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full class reference-data collection — the authored class catalogue the admin Workbench
    /// edits and the create-character screen previews (the class picker lands in #1225).
    /// </summary>
    public class GetClasses : AbstractReferenceDataCommand<Class>
    {
        private readonly IClasses _classes;

        public override string Name { get; set; } = nameof(GetClasses);

        public GetClasses(IClasses classes)
        {
            _classes = classes;
        }

        protected override IEnumerable<Class> GetReferenceData()
        {
            return _classes.All();
        }

        protected override object VersionKey => _classes.VersionKey;
    }
}
