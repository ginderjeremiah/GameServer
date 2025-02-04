using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IUsers
    {
        Task<User?> GetUser(string username);
        public Task<bool> CheckIfUsernameExists(string userName);
    }
}
