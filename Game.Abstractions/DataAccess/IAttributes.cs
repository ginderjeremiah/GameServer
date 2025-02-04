namespace Game.Abstractions.DataAccess
{
    public interface IAttributes
    {
        public IAsyncEnumerable<Attribute> All();
    }
}
