using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Attributes
{
    public class Attribute : IEntity
    {
        public int AttributeId { get; set; }
        public string AttributeName { get; set; }
        public string AttributeDesc { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            AttributeId = record["AttributeId"].AsInt();
            AttributeName = record["AttributeName"].AsString();
            AttributeDesc = record["AttributeDesc"].AsString();
        }
    }
}
