using GameCore;
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
        /// TODO: make this not an extension...
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public static string GetNewToken(this Session session)
        {
            var tokenData = $"{session.SessionId.ToBase64()}.{DateTime.UtcNow.Add(Constants.TOKEN_LIFETIME).Ticks.ToBase64()}";
            return $"{tokenData}.{tokenData.Hash(session.Player.Salt.ToString(), 1).ToBase64()}";
        }

        /// <summary>
        /// Creates a <see cref="PlayerData"/> object from a <see cref="Session"/>.
        /// </summary>
        /// <param name="session"></param>
        /// <returns>A <see cref="PlayerData"/> object.</returns>
        public static PlayerData GetPlayerData(this Session session)
        {
            return new(session.Player, session.InventoryData);
        }
    }
}
