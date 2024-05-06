using System.Data;

namespace GameCore.Infrastructure
{
    public class StructuredData
    {
        public List<ValueTuple<string, DbType>> Columns { get; } = new();
        public List<List<object?>> Rows { get; } = new();

        public void AddColumn(ValueTuple<string, DbType> column)
        {
            if (Rows.Any())
            {
                throw new ArgumentException($"Could not add column to {nameof(StructuredData)}.  Columns cannot be added if any rows exist.");
            }

            Columns.Add(column);
        }

        public void AddRow(List<object?> row)
        {
            if (row.Count != Columns.Count)
            {
                throw new ArgumentException($"Could not add row to {nameof(StructuredData)}.  The number of row values does not match the number of columns.");
            }

            Rows.Add(row);
        }
    }
}
