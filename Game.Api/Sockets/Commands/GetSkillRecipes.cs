using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full skill-synthesis recipe reference-data set over the socket (spike #1125). The client
    /// derives each recipe's availability and hint state from this set plus the player's owned skills and
    /// proficiency levels, so no separate player-data command is needed.
    /// </summary>
    public class GetSkillRecipes : AbstractReferenceDataCommand<SkillRecipe>
    {
        private readonly ISkillRecipes _skillRecipes;

        public override string Name { get; set; } = nameof(GetSkillRecipes);

        public GetSkillRecipes(ISkillRecipes skillRecipes)
        {
            _skillRecipes = skillRecipes;
        }

        protected override IEnumerable<SkillRecipe> GetReferenceData()
        {
            return _skillRecipes.AllSkillRecipes();
        }

        protected override object VersionKey => _skillRecipes.VersionKey;
    }
}
