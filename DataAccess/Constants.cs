namespace DataAccess
{
    internal class Constants
    {
        public const string REDIS_SESSION_PREFIX = "Session";
        public const string REDIS_PLAYER_CHANNEL = "player";
        public const string REDIS_PLAYER_QUEUE = "PlayerUpdateQueue";
        public const string REDIS_SKILLS_CHANNEL = "skills";
        public const string REDIS_SKILLS_QUEUE = "SkillsUpdateQueue";
        public const string REDIS_INVENTORY_CHANNEL = "inventory";
        public const string REDIS_INVENTORY_QUEUE = "InventoryUpdateQueue";
        public const string REDIS_EQUIPPED_CHANNEL = "equipped";
        public const string REDIS_EQUIPPED_QUEUE = "EquippedUpdateQueue";
    }
}
