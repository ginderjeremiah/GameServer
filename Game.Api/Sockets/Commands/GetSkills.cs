using Game.Abstractions.DataAccess;
using Game.Api.Models.Skills;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full skill reference-data collection. WebSocket equivalent of
    /// the <c>GET /api/Skills</c> endpoint.
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
            return _skills.AllSkills().To().Model<Skill>();
        }
    }
}
