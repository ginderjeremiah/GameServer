namespace Game.DataAccess
{
    internal class Constants
    {
        // Cache Keys
        internal const string CACHE_SESSION_PREFIX = "Session";
        internal const string CACHE_BATTLE_DATA_PREFIX = "BattleData";
        internal const string CACHE_PLAYER_PREFIX = "Player";

        // Pub/Sub Channels/Queues
        internal const string PUBSUB_PLAYER_CHANNEL = "player";
        internal const string PUBSUB_PLAYER_QUEUE = "PlayerUpdateQueue";
        internal const string PUBSUB_SKILLS_CHANNEL = "skills";
        internal const string PUBSUB_SKILLS_QUEUE = "SkillsUpdateQueue";
        internal const string PUBSUB_INVENTORY_CHANNEL = "inventory";
        internal const string PUBSUB_INVENTORY_QUEUE = "InventoryUpdateQueue";
    }
}
