using Game.Abstractions;

namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Reports whether the authenticated player already has a live game connection (an open websocket)
    /// somewhere else. The login flow checks this before entering the game so it can warn the user that
    /// continuing here will disconnect the other session.
    /// </summary>
    public class ActiveSessionResult : IModel
    {
        public bool Active { get; set; }
    }
}
