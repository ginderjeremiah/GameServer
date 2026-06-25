using Game.Abstractions.DataAccess;
using Path = Game.Abstractions.Contracts.Path;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full path reference-data collection — the ordered proficiency tracks (each with its
    /// skill contributions) the client renders the proficiency tree's rails and "Trained by" chips from.
    /// Version-keyed off the same proficiency snapshot as <see cref="GetProficiencies"/>.
    /// </summary>
    public class GetPaths : AbstractReferenceDataCommand<Path>
    {
        private readonly IProficiencies _proficiencies;

        public override string Name { get; set; } = nameof(GetPaths);

        public GetPaths(IProficiencies proficiencies)
        {
            _proficiencies = proficiencies;
        }

        protected override IEnumerable<Path> GetReferenceData()
        {
            return _proficiencies.AllPaths();
        }

        protected override object VersionKey => _proficiencies.VersionKey;
    }
}
