using System.Text.Json.Serialization;

namespace GameServer.Models.Common
{
    public class ApiSocketResponse
    {
        public string? Id { get; set; }
        public string? Error { get; set; }
        [JsonIgnore]
        public ESocketCloseReason? CloseReason { get; set; }
    }

    public class ApiSocketResponse<T> : ApiSocketResponse
    {
        public T Data { get; set; }
    }
}
