namespace GameServer.Models.Common
{
    public class ApiResponse<T> : IApiResponse where T : IModel
    {
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ApiListResponse<T> : IApiListResponse<T> where T : IModel
    {
        public List<T>? Data { get; set; }
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

        public static ApiListResponse<T> Success<T>(IEnumerable<T> data) where T : IModel
        {
            return new ApiListResponse<T>
            {
                Data = data.ToList()
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

        public static ApiListResponse<T> ErrorWithListData<T>(string message, List<T> data) where T : IModel
        {
            return new ApiListResponse<T>
            {
                Data = data,
                ErrorMessage = message
            };
        }
    }

    public interface IApiListResponse<T> : IApiListResponse where T : IModel
    {
        public List<T>? Data { get; set; }
    }

    public interface IApiListResponse : IApiResponse { }

    public interface IApiResponse
    {
        public string? ErrorMessage { get; set; }
    }
}
