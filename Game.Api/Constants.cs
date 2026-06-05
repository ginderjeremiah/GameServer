namespace Game.Api
{
    public static class Constants
    {
        public static readonly TimeSpan ACCESS_TOKEN_LIFETIME = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan REFRESH_TOKEN_LIFETIME = TimeSpan.FromHours(48);
        public const string SERVER_PRINCIPAL = "game-server-api";

        public const string CACHE_PLAYER_SOCKET_PREFIX = "PlayerSocket";
        public const string PUBSUB_SOCKET_QUEUE_PREFIX = "SocketQueue";
        public const string PUBSUB_SOCKET_CHANNEL_PREFIX = "SocketChannel";
    }
}
