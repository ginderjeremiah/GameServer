namespace GameServer.Models.Common
{
    public class ApiResponse<T> : IApiResponse where T : IModel
    {
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    public class ApiListResponse<T> : IApiListResponse where T : IModel
    {
        public List<T>? Data { get; set; }
        public string? Error { get; set; }
    }

    public class ApiResponse : IApiResponse
    {
        public string? Error { get; set; }
    }

    public interface IApiListResponse : IApiResponse
    {
    }

    public interface IApiResponse
    {
        public string? Error { get; set; }
    }
}
