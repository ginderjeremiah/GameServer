namespace GameServer.Models.Common
{
    public class ApiResponse<T> : IApiResponse
    {
        public T? Data { get; set; }
        public string? Error { get; set; }
    }

    public class ApiResponse : IApiResponse
    {
        public string? Error { get; set; }
    }

    public interface IApiResponse
    {
        public string? Error { get; set; }
    }
}
