//using Microsoft.AspNetCore.Mvc;

//namespace GameServer.Controllers
//{
//    public class BaseController : Controller
//    {
//        //private long _beginTimestamp;
//        //private string? _route;
//        //private readonly SessionService _sessionService;

//        //protected Session Session => _sessionService.GetSession();
//        //protected bool SessionAvailable => _sessionService.SessionAvailable;
//        //protected int PlayerId => Session.Player.Id;
//        //protected IRepositoryManager Repositories { get; }
//        //protected IApiLogger Logger { get; }

//        //public BaseController(IRepositoryManager repositoryManager, SessionService sessionService)
//        //{
//        //    Repositories = repositoryManager;
//        //    _sessionService = sessionService;
//        //}

//        //[NonAction]
//        //public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
//        //{
//        //    _beginTimestamp = Stopwatch.GetTimestamp();
//        //    _route = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
//        //    Logger.LogInfo($"Begin {_route} request");

//        //    var resultContext = await next();

//        //    if (resultContext.Exception is not null)
//        //    {
//        //        Logger.LogError(resultContext.Exception);
//        //    }

//        //    if (SessionAvailable)
//        //    {
//        //        await Session.Save();
//        //    }

//        //    Logger.LogInfo($"End {_route} request: {Stopwatch.GetElapsedTime(_beginTimestamp).TotalMilliseconds} ms");
//        //}

//        //[NonAction]
//        //protected ApiResponse Success()
//        //{
//        //    return new ApiResponse();
//        //}

//        //[NonAction]
//        //protected ApiResponse<T> Success<T>(T data) where T : IModel
//        //{
//        //    return new ApiResponse<T>
//        //    {
//        //        Data = data
//        //    };
//        //}

//        //[NonAction]
//        //protected ApiListResponse<T> Success<T>(IEnumerable<T> data) where T : IModel
//        //{
//        //    return new ApiListResponse<T>
//        //    {
//        //        Data = data.ToList()
//        //    };
//        //}

//        //[NonAction]
//        //protected ApiResponse Error(string message)
//        //{
//        //    Response.StatusCode = StatusCodes.Status400BadRequest;
//        //    return new ApiResponse
//        //    {
//        //        Error = message
//        //    };
//        //}

//        //[NonAction]
//        //protected ApiResponse<T> Error<T>(string message) where T : IModel
//        //{
//        //    Response.StatusCode = StatusCodes.Status400BadRequest;
//        //    return new ApiResponse<T>
//        //    {
//        //        Error = message
//        //    };
//        //}

//        //[NonAction]
//        //protected ApiResponse<T> ErrorWithData<T>(string message, T data) where T : IModel
//        //{
//        //    Response.StatusCode = StatusCodes.Status400BadRequest;
//        //    return new ApiResponse<T>
//        //    {
//        //        Data = data,
//        //        Error = message
//        //    };
//        //}

//        //[NonAction]
//        //protected ApiListResponse<T> ErrorWithListData<T>(string message, List<T> data) where T : IModel
//        //{
//        //    Response.StatusCode = StatusCodes.Status400BadRequest;
//        //    return new ApiListResponse<T>
//        //    {
//        //        Data = data,
//        //        Error = message
//        //    };
//        //}
//    }
//}
