namespace Game.Api.Models.Common
{
    public class ApiResponse<T> : IApiResponse where T : IModel
    {
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ApiEnumerableResponse<T> : IApiEnumerableResponse<T> where T : IModel
    {
        public IEnumerable<T>? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ApiAsyncEnumerableResponse<T> : IApiAsyncEnumerableResponse<T> where T : IModel
    {
        public IAsyncEnumerable<T>? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ApiResponse : IApiResponse
    {
        public string? ErrorMessage { get; set; }

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

        public static ApiResponse<T> Error<T>(string message) where T : IModel
        {
            return new ApiResponse<T>
            {
                ErrorMessage = message
            };
        }

        public static ApiResponse<T> ErrorWithData<T>(string message, T data) where T : IModel
        {
            return new ApiResponse<T>
            {
                Data = data,
                ErrorMessage = message
            };
        }

        public static ApiEnumerableResponse<T> ErrorWithListData<T>(string message, List<T> data) where T : IModel
        {
            return new ApiEnumerableResponse<T>
            {
                Data = data,
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
