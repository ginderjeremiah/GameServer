namespace GameServer
{
    public static class Constants
    {
        public static TimeSpan TOKEN_LIFETIME = TimeSpan.FromDays(1);
        public const string TOKEN_NAME = "sessionToken";

        public const string CACHE_PLAYER_SOCKET_PREFIX = "PlayerSocket";
        public const string PUBSUB_SOCKET_QUEUE_PREFIX = "SocketQueue";
        public const string PUBSUB_SOCKET_CHANNEL_PREFIX = "SocketChannel";
    }
}
