namespace Game.Abstractions.Infrastructure
{
    public enum CacheSystem
    {
        Redis = 0
    }

    public enum DatabaseSystem
    {
        // Deliberately starts at 1 so an unset/missing config binds to the unnamed default (0) and fails loud
        // in GameContextFactory rather than silently selecting a provider (the app is Postgres-only).
        Postgres = 1
    }

    public enum PubSubSystem
    {
        Redis = 0
    }
}
