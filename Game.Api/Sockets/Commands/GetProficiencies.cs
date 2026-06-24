using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full proficiency reference-data collection — the authored proficiency catalogue the
    /// client builds the proficiency tree from and composes the live battler's per-level/milestone
    /// attribute bonuses out of (mirroring the backend battle snapshot).
    /// </summary>
    public class GetProficiencies : AbstractReferenceDataCommand<Proficiency>
    {
        private readonly IProficiencies _proficiencies;

        public override string Name { get; set; } = nameof(GetProficiencies);

        public GetProficiencies(IProficiencies proficiencies)
        {
            _proficiencies = proficiencies;
        }

        protected override IEnumerable<Proficiency> GetReferenceData()
        {
            return _proficiencies.AllProficiencies();
        }

        protected override object VersionKey => _proficiencies.VersionKey;
    }
}
