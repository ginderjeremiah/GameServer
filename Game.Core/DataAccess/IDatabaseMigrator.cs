namespace Game.Core.DataAccess
{
    public interface IDatabaseMigrator
    {
        public Task Migrate();
    }
}
