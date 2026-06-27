using Game.Abstractions.DataAccess;
using Game.Core.Players;

namespace Game.Application.Services
{
    /// <summary>
    /// Orchestrates the player-initiated skill-synthesis command (spike #1125, area B): resolves the recipe and
    /// its result skill from the reference caches and the player's proficiency levels from the progress
    /// aggregate, then delegates the authoritative anti-cheat validation and the idempotent unlock to the
    /// domain (<see cref="Player.TrySynthesizeSkill"/>), persisting only on success. Holds no domain logic of
    /// its own — just the wiring across the caches, the progress aggregate, and the player repository.
    /// </summary>
    public class SynthesisService(
        IPlayerRepository playerRepo,
        ISkillRecipes skillRecipes,
        ISkills skills,
        IPlayerProgressRepository playerProgress)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly ISkillRecipes _skillRecipes = skillRecipes;
        private readonly ISkills _skills = skills;
        private readonly IPlayerProgressRepository _playerProgress = playerProgress;

        /// <summary>
        /// Synthesizes the recipe identified by <paramref name="recipeId"/> for <paramref name="player"/>,
        /// returning the unlocked result skill id on success or <c>null</c> when the synthesis is rejected
        /// (unknown or retired recipe, a missing input, or an unmet proficiency condition) — a rejection
        /// persists nothing.
        /// </summary>
        public async Task<int?> SynthesizeSkill(Player player, int recipeId, CancellationToken cancellationToken = default)
        {
            // A tampered client controls only the recipe id, so bounds-check it up front rather than letting
            // the indexed GetSkillRecipe throw (which would surface as a 500, not a clean rejection).
            if (!_skillRecipes.ValidateRecipeId(recipeId))
            {
                return null;
            }

            var recipe = _skillRecipes.GetSkillRecipe(recipeId);
            var resultSkill = _skills.GetSkill(recipe.ResultSkillId);

            // The recipe's conditions gate on proficiency levels, which live on the separate PlayerProgress
            // aggregate (mirroring the gear proficiency gate in PlayerService.EquipItem).
            var proficiencyLevels = await _playerProgress.GetProficiencies(player.Id, cancellationToken);
            var levelsByProficiency = proficiencyLevels.ToDictionary(p => p.ProficiencyId, p => p.Level);

            if (!player.TrySynthesizeSkill(recipe, resultSkill, levelsByProficiency))
            {
                return null;
            }

            await _playerRepo.SavePlayer(player, cancellationToken);
            return resultSkill.Id;
        }
    }
}
