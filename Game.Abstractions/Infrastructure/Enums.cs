namespace Game.Abstractions.Infrastructure
{
    public enum CacheSystem
    {
        // Deliberately starts at 0, unlike DatabaseSystem: an unset config selects Redis rather than failing
        // loud. A missing connection string still throws in RedisMultiplexerFactory, so this stays safe.
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
        // Deliberately starts at 0, unlike DatabaseSystem: an unset config selects Redis rather than failing
        // loud. A missing connection string still throws in RedisMultiplexerFactory, so this stays safe.
        Redis = 0
    }
}
