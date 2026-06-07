using Game.Core;

namespace Game.Api.Models.Player
{
    public class LogPreference : IModel
    {
        public ELogType Id { get; set; }
        public bool Enabled { get; set; }
    }
}
