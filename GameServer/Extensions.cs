using GameCore;
using GameCore.Sessions;
using GameServer.Models.Player;

namespace GameServer
{
    public static class Extensions
    {
        public static string GetNewToken(this Session session)
        {
            var tokenData = $"{session.SessionId.ToBase64()}.{DateTime.UtcNow.Add(Constants.TOKEN_LIFETIME).Ticks.ToBase64()}";
            return $"{tokenData}.{tokenData.Hash(session.Player.Salt.ToString(), 1).ToBase64()}";
        }

        public static PlayerData GetPlayerData(this Session session)
        {
            return new(session.Player, session.InventoryData);
        }
    }
}
