namespace Game.Abstractions.DataAccess
{
    public interface IDatabaseMigrator
    {
        public Task Migrate(bool resetDatabase = false);
    }
}
