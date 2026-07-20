namespace Game.Api
{
    public static class Constants
    {
        public const string SERVER_PRINCIPAL = "game-server-api";

        public const string CACHE_PLAYER_SOCKET_PREFIX = "PlayerSocket";
        public const string CACHE_ACCOUNT_SOCKET_PREFIX = "AccountSocket";
        public const string PUBSUB_SOCKET_QUEUE_PREFIX = "SocketQueue";
        public const string PUBSUB_SOCKET_CHANNEL_PREFIX = "SocketChannel";

        // Holding list for a server-initiated (pub/sub) socket command that failed execution, so a poisoned
        // push is preserved for inspection/replay instead of being silently dropped — the same "never
        // silently drop" philosophy the player write-behind dead-letter queue follows (#671).
        public const string PUBSUB_SOCKET_DEAD_LETTER_QUEUE = "SocketCommandDeadLetterQueue";
    }
}
