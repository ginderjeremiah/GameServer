using DataAccess;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Attribute = DataAccess.Models.Attributes.Attribute;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AttributesController : BaseController
    {
        public AttributesController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiResponse<List<Attribute>> Attributes()
        {
            return Success(Repositories.Attributes.AllAttributes());
        }
    }
}
