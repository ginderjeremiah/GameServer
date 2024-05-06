using System.Data;

namespace GameCore.Entities
{
    public interface IEntity
    {
        public void LoadFromReader(IDataRecord record);
    }
}
