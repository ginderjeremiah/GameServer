using System.Data;

namespace GameCore.Database.Interfaces
{
    public interface IEntity
    {
        public void LoadFromReader(IDataRecord record);
    }
}
