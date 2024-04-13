using DataAccess;
using GameLibrary.Logging;
using GameServer.Auth;
using GameServer.Models;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace GameServer.Controllers
{
    public class BaseController : Controller
    {
        private readonly IApiLogger _logger;
        private Session? _session;
        private long _beginTimestamp;
        private string? _route;

        //HttpContext.Items["Session"] is populated via SessionAuthorize attribute;
        //Session can only be null if SessionAuthorize is not used or (AllowAll = true) is specified in the SessionAuthorize attribute;
        protected Session Session => _session ??= (Session?)HttpContext.Items["Session"];
        protected int PlayerId => Session.Player.PlayerId;
        protected IRepositoryManager Repositories { get; }
        protected CookieOptions DefaultCookieOptions
        {
            get
            {
                return new CookieOptions()
                {
                    Secure = true,
                    HttpOnly = true,
                    Expires = DateTime.UtcNow.AddDays(1)
                };
            }
        }

        public BaseController(IRepositoryManager repositoryManager, IApiLogger logger)
        {
            Repositories = repositoryManager;
            _logger = logger;
        }

        [NonAction]

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            _beginTimestamp = Stopwatch.GetTimestamp();
            _route = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
            Log($"Begin {_route} request");
        }

        [NonAction]
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception is not null)
            {
                LogError(context.Exception);
            }
            Session?.Save();
            Log($"End {_route} request: {Stopwatch.GetElapsedTime(_beginTimestamp).TotalMilliseconds} ms");
        }

        [NonAction]
        protected void Log(string message)
        {
            _logger.Log(message);
        }

        [NonAction]
        protected void LogError(string message)
        {
            _logger.LogError(message);
        }

        [NonAction]
        protected void LogError(Exception exception)
        {
            _logger.LogError(exception);
        }

        [NonAction]
        protected ApiResponse Success()
        {
            return new ApiResponse();
        }

        [NonAction]
        protected ApiResponse<T> Success<T>(T data) where T : IModel
        {
            return new ApiResponse<T>
            {
                Data = data
            };
        }

        [NonAction]
        protected ApiListResponse<T> Success<T>(IEnumerable<T> data) where T : IModel
        {
            return new ApiListResponse<T>
            {
                Data = data.ToList()
            };
        }

        [NonAction]
        protected ApiResponse Error(string message)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new ApiResponse
            {
                Error = message
            };
        }

        [NonAction]
        protected ApiResponse<T> Error<T>(string message) where T : IModel
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new ApiResponse<T>
            {
                Error = message
            };
        }

        [NonAction]
        protected ApiResponse<T> ErrorWithData<T>(string message, T data) where T : IModel
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new ApiResponse<T>
            {
                Data = data,
                Error = message
            };
        }

        [NonAction]
        protected ApiListResponse<T> ErrorWithListData<T>(string message, List<T> data) where T : IModel
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new ApiListResponse<T>
            {
                Data = data,
                Error = message
            };
        }
    }
}
