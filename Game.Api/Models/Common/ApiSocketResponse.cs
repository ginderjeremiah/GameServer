namespace Game.Api.Models.Common
{
    public class ApiSocketResponse
    {
        public string? Id { get; set; }
        public string? Error { get; set; }
    }

    public class ApiSocketResponse<T> : ApiSocketResponse
    {
        public T Data { get; set; }
    }
}
