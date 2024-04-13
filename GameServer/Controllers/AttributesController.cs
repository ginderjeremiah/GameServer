using DataAccess;
using GameLibrary.Logging;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Attribute = GameServer.Models.Attributes.Attribute;

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
        public ApiListResponse<Attribute> Attributes()
        {
            return Success(Repositories.Attributes.AllAttributes().Select(att => new Attribute(att)).ToList());
        }
    }
}
