using System.Data;
using System.Diagnostics.CodeAnalysis;
using static System.Data.ParameterDirection;

namespace GameCore.Infrastructure
{
    public class QueryParameter
    {
        private object? _value;
        private readonly StructuredData? _data;

        public string ParameterName { get; }
        public object? Value { get => GetValue(); set => SetValue(value); }
        public DbType? Type { get; private set; }
        public ParameterDirection Direction { get; }

        public QueryParameter(string parameterName, object? value, DbType? dbType = null, ParameterDirection direction = Input)
        {
            ParameterName = parameterName;
            _value = value;
            Direction = direction;
            Type = dbType;
        }

        public QueryParameter(string parameterName, StructuredType type, ParameterDirection direction = Input)
        {
            ParameterName = parameterName;
            Direction = direction;
            _data = new(type);
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
            EnsureDataExists();
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
            EnsureDataExists();
            _data.AddRow(row);
        }

        private object? GetValue()
        {
            return _data ?? _value;
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

        [MemberNotNull(nameof(_data))]
        private void EnsureDataExists()
        {
            if (_data is null)
            {
                throw new InvalidOperationException("Cannot perform operation when not using structured data.");
            }
        }
    }
}
