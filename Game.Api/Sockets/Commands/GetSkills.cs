using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full skill reference-data set over the socket.
    /// </summary>
    public class GetSkills : AbstractReferenceDataCommand<Skill>
    {
        private readonly ISkills _skills;

        public override string Name { get; set; } = nameof(GetSkills);

        public GetSkills(ISkills skills)
        {
            _skills = skills;
        }

        protected override IEnumerable<Skill> GetReferenceData()
        {
            return _skills.AllSkills();
        }

        protected override object VersionKey => _skills.VersionKey;
    }
}
