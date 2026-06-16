using Game.Abstractions.DataAccess.Admin;

namespace Game.Api.Models.Common
{
    public class ApiResponse<T> : IApiResponse where T : IModel
    {
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public static implicit operator ApiResponse<T>(ApiResponse result)
        {
            return new()
            {
                ErrorMessage = result.ErrorMessage,
            };
        }
    }

    public class ApiEnumerableResponse<T> : IApiEnumerableResponse<T> where T : IModel
    {
        public IEnumerable<T>? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public static implicit operator ApiEnumerableResponse<T>(ApiResponse result)
        {
            return new()
            {
                ErrorMessage = result.ErrorMessage,
            };
        }
    }

    public class ApiAsyncEnumerableResponse<T> : IApiAsyncEnumerableResponse<T> where T : IModel
    {
        public IAsyncEnumerable<T>? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public static implicit operator ApiAsyncEnumerableResponse<T>(ApiResponse result)
        {
            return new()
            {
                ErrorMessage = result.ErrorMessage,
            };
        }
    }

    public class ApiResponse : IApiResponse
    {
        public string? ErrorMessage { get; set; }

        // Maps the admin data tier's unified write result to the API response once, so every admin
        // endpoint can hand its result back directly instead of re-deriving the success/error mapping.
        public static implicit operator ApiResponse(AdminSaveResult result)
        {
            return new ApiResponse
            {
                ErrorMessage = result.ErrorMessage
            };
        }

        public static ApiResponse Success()
        {
            return new ApiResponse();
        }

        public static ApiResponse<T> Success<T>(T data) where T : IModel
        {
            return new ApiResponse<T> { Data = data };
        }

        public static ApiEnumerableResponse<T> Success<T>(IEnumerable<T> data) where T : IModel
        {
            return new ApiEnumerableResponse<T>
            {
                Data = data
            };
        }

        public static ApiAsyncEnumerableResponse<T> Success<T>(IAsyncEnumerable<T> data) where T : IModel
        {
            return new ApiAsyncEnumerableResponse<T>
            {
                Data = data
            };
        }

        public static ApiResponse Error(string message)
        {
            return new ApiResponse
            {
                ErrorMessage = message
            };
        }
    }

    public interface IApiAsyncEnumerableResponse<T> : IApiCollectionResponse where T : IModel
    {
        public IAsyncEnumerable<T>? Data { get; set; }
    }

    public interface IApiEnumerableResponse<T> : IApiCollectionResponse where T : IModel
    {
        public IEnumerable<T>? Data { get; set; }
    }

    public interface IApiCollectionResponse : IApiResponse { }

    public interface IApiResponse
    {
        public string? ErrorMessage { get; set; }
    }
}
