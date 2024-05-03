using System.Data;
using System.Data.SqlClient;
using static System.Data.ParameterDirection;

namespace GameLibrary.Database
{
    public class QueryParameter
    {
        public string ParameterName { get; }
        public object? Value { get; }
        public DbType? Type { get; }
        public ParameterDirection Direction { get; }

        public QueryParameter(string parameterName, object? value, DbType? dbType = null, ParameterDirection direction = Input)
        {
            ParameterName = parameterName;
            Value = value;
            Direction = direction;
            Type = dbType;
        }

        internal virtual SqlParameter GetSqlParameter()
        {
            if (Type == null)
            {
                return new SqlParameter(ParameterName, Value)
                {
                    Direction = Direction,
                };
            }
            else
            {
                return new SqlParameter(ParameterName, Type)
                {
                    Value = Value,
                    Direction = Direction,
                };
            }
        }
    }
}
