using GameCore;
using GameServer.Models.Common;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Attribute = GameServer.Models.Attributes.Attribute;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AttributesController : BaseController
    {
        public AttributesController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Attribute> Attributes()
        {
            return Success(Repositories.Attributes.AllAttributes().Select(att => new Attribute(att)).ToList());
        }
    }
}
