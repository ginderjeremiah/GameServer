using System.Data;

namespace GameLibrary.Database.Interfaces
{
    public interface IEntity
    {
        public void LoadFromReader(IDataRecord record);
    }
}
