using GameCore.Sessions;
using GameServer.Models.Player;

namespace GameServer
{
    /// <summary>
    /// A class to house generic extensions used within the project.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Creates a <see cref="PlayerData"/> object from a <see cref="Session"/>.
        /// </summary>
        /// <param name="session"></param>
        /// <returns>A <see cref="PlayerData"/> object.</returns>
        public static PlayerData GetPlayerData(this Session session)
        {
            return new(session.Player, session.InventoryData, session.CurrentZone);
        }

        public static string GetDescription(this ESocketCloseReason reason)
        {
            return reason switch
            {
                ESocketCloseReason.Inactivity => "The socket has been closed due to inactivity.",
                ESocketCloseReason.SocketReplaced => "The socket has been closed because a new one was established.",
                //ESocketCloseReason.Finished
                _ => "The socket has been closed normally."
            };
        }
    }
}
