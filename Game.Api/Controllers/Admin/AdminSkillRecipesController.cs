using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting skill-synthesis recipes and their related collections (input
    /// skills and proficiency-level conditions). A thin HTTP adapter over <see cref="IAdminSkillRecipes"/>. The
    /// route prefix is shared across every admin controller so the existing <c>/api/AdminTools/*</c> contract is
    /// preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminSkillRecipesController(IAdminSkillRecipes adminSkillRecipes) : ControllerBase
    {
        private readonly IAdminSkillRecipes _adminSkillRecipes = adminSkillRecipes;

        [HttpPost]
        public ApiResponse AddEditSkillRecipes([FromBody] List<Change<SkillRecipe>> changes)
        {
            return _adminSkillRecipes.SaveSkillRecipes(changes);
        }

        [HttpPost]
        public ApiResponse SetSkillRecipeInputs([FromBody] SetSkillRecipeInputsData changeData)
        {
            return _adminSkillRecipes.SetInputs(changeData);
        }

        [HttpPost]
        public ApiResponse SetSkillRecipeConditions([FromBody] SetSkillRecipeConditionsData changeData)
        {
            return _adminSkillRecipes.SetConditions(changeData);
        }
    }
}
