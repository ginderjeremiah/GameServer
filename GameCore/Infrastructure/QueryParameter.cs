using System.Data;
using static System.Data.ParameterDirection;

namespace GameCore.Infrastructure
{
    public class QueryParameter
    {
        private object? _value;
        private readonly StructuredData _data = new();

        public string ParameterName { get; }
        public object? Value { get => GetValue(); set => SetValue(value); }
        public DbType? Type { get; private set; }
        public ParameterDirection Direction { get; }
        public string? TypeName { get; }


        public QueryParameter(string parameterName, object? value, DbType? dbType = null, ParameterDirection direction = Input)
        {
            ParameterName = parameterName;
            _value = value;
            Direction = direction;
            Type = dbType;
        }

        public QueryParameter(string parameterName, string typeName, ParameterDirection direction = Input)
        {
            ParameterName = parameterName;
            TypeName = typeName;
            Direction = direction;
            Type = DbType.Object;
        }

        public void AddColumns(params ValueTuple<string, DbType>[] columns)
        {
            foreach (var column in columns)
            {
                AddColumn(column);
            }
        }

        public void AddColumn(ValueTuple<string, DbType> column)
        {
            Type = DbType.Object;
            _data.AddColumn(column);
        }

        public void AddRows(List<List<object?>> rows)
        {
            foreach (var row in rows)
            {
                AddRow(row);
            }
        }

        public void AddRow(List<object?> row)
        {
            Type = DbType.Object;
            _data.AddRow(row);
        }

        private object? GetValue()
        {
            return Type == DbType.Object ? _data : _value;
        }

        private void SetValue(object? value)
        {
            if (Type == DbType.Object)
            {
                throw new InvalidOperationException("Cannot set parameter value when using structured data.");
            }
            else
            {
                _value = value;
            }
        }
    }
}
