namespace Game.Application
{
    public interface IUnitOfWork
    {
        Task CommitAsync();
    }
}
