using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    public interface ITags
    {
        public IAsyncEnumerable<Contracts.Tag> All();
    }
}
