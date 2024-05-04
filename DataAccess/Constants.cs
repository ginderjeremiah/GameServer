namespace DataAccess
{
    internal class Constants
    {
        //Cache Keys
        public const string CACHE_SESSION_PREFIX = "Session";
        public const string CACHE_ACTIVE_ENEMY_PREFIX = "ActiveEnemy";

        //Pub/Sub Channels/Queues
        public const string PUBSUB_PLAYER_CHANNEL = "player";
        public const string PUBSUB_PLAYER_QUEUE = "PlayerUpdateQueue";
        public const string PUBSUB_SKILLS_CHANNEL = "skills";
        public const string PUBSUB_SKILLS_QUEUE = "SkillsUpdateQueue";
        public const string PUBSUB_INVENTORY_CHANNEL = "inventory";
        public const string PUBSUB_INVENTORY_QUEUE = "InventoryUpdateQueue";
    }
}
