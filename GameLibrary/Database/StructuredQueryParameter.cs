using Microsoft.SqlServer.Server;
using System.Data;
using System.Data.SqlClient;
using static System.Data.ParameterDirection;

namespace GameLibrary.Database
{
    public class StructuredQueryParameter : QueryParameter
    {
        public string TypeName { get; }
        public List<ValueTuple<string, DbType>> Columns { get; } = new();
        public List<List<object?>> Rows { get; } = new();

        public StructuredQueryParameter(string parameterName, string typeName, ParameterDirection direction = Input) : base(parameterName, null, null, direction)
        {
            TypeName = typeName;
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
            if (Rows.Any())
            {
                throw new ArgumentException($"Could not add column to {nameof(StructuredQueryParameter)}.  Columns cannot be added if any rows exist.");
            }

            Columns.Add(column);
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
            if (row.Count != Columns.Count)
            {
                throw new ArgumentException($"Could not add row to {nameof(StructuredQueryParameter)}.  The number of row values does not match the number of columns.");
            }

            Rows.Add(row);
        }

        internal override SqlParameter GetSqlParameter()
        {
            var metaData = new SqlMetaData[Columns.Count];

            for (int i = 0; i < Columns.Count; i++)
            {
                //use temp parameter to convert DbType to SqlDbType
                var tempParameter = new SqlParameter("temp", null) { DbType = Columns[i].Item2 };
                metaData[i] = new SqlMetaData(Columns[i].Item1, tempParameter.SqlDbType);
            }

            var data = Rows.Select(row =>
            {
                var record = new SqlDataRecord(metaData);

                for (int i = 0; i < Columns.Count; i++)
                {
                    record.SetValue(i, row[i]);
                }

                return record;
            }).ToArray();

            return new SqlParameter(ParameterName, SqlDbType.Structured)
            {
                Value = data.Length > 0 ? data : null,
                TypeName = TypeName
            };
        }
    }
}
