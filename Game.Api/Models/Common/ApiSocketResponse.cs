namespace Game.Api.Models.Common
{
    public class ApiSocketResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Error { get; set; }
    }

    public class ApiSocketResponse<T> : ApiSocketResponse
    {
        public required T Data { get; set; }
    }
}
