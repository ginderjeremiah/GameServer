using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Game.DataAccess.Content
{
    /// <summary>
    /// Bulk-inserts entity rows with their <em>explicit</em> primary-key values (including id <c>0</c>, which
    /// EF's normal insert path treats as "let the store generate" for a zero-based identity column, so a plain
    /// <c>Add</c>/<c>SaveChanges</c> could not preserve it). It builds a parameterized multi-row INSERT from the
    /// EF model metadata — table and column names come from the model, so the SQL cannot drift from the schema —
    /// and reads values straight off the CLR entity, bypassing change tracking. Used only by the trusted content
    /// seeder against a fresh database.
    /// </summary>
    internal static class EntityRowInserter
    {
        // Keep each statement well under PostgreSQL's 65535-parameter cap; the content sets are tiny anyway.
        private const int ChunkSize = 500;

        /// <summary>Inserts <paramref name="rows"/> (all of the same mapped entity type) with their explicit
        /// column values. Null values are emitted as SQL <c>NULL</c> literals so a null needs no typed
        /// parameter; every non-null value is parameterized.</summary>
        public static async Task InsertAsync<T>(GameContext context, IReadOnlyList<T> rows, CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var entityType = context.Model.FindEntityType(typeof(T))
                ?? throw new InvalidOperationException($"'{typeof(T)}' is not a mapped entity type.");
            var table = entityType.GetTableName()
                ?? throw new InvalidOperationException($"Entity '{entityType.DisplayName()}' is not mapped to a table.");
            var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());

            var columns = entityType.GetProperties()
                .Select(property => (
                    Name: property.GetColumnName(storeObject),
                    Accessor: property.PropertyInfo ?? throw new InvalidOperationException(
                        $"Property '{entityType.DisplayName()}.{property.Name}' has no CLR accessor; the content " +
                        "seeder cannot read shadow properties.")))
                .Where(column => column.Name is not null)
                .ToList();

            var columnList = string.Join(", ", columns.Select(column => $"\"{column.Name}\""));

            foreach (var chunk in rows.Chunk(ChunkSize))
            {
                var parameters = new List<object>();
                var valueTuples = new List<string>(chunk.Length);
                foreach (var row in chunk)
                {
                    var placeholders = new List<string>(columns.Count);
                    foreach (var column in columns)
                    {
                        var value = column.Accessor.GetValue(row);
                        if (value is null)
                        {
                            placeholders.Add("NULL");
                        }
                        else
                        {
                            placeholders.Add($"{{{parameters.Count}}}");
                            parameters.Add(value);
                        }
                    }

                    valueTuples.Add($"({string.Join(", ", placeholders)})");
                }

                var sql = $"INSERT INTO \"{table}\" ({columnList}) VALUES {string.Join(", ", valueTuples)}";
                await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
            }
        }

        /// <summary>
        /// Advances the identity sequence of each inserted table past its highest explicit id, so a later
        /// DB-generated insert (e.g. authoring a record through the admin tools) does not collide with a seeded
        /// row — the same fix-up <c>e2e-seed.sql</c> did by hand. A table with a single integer identity primary
        /// key is advanced; composite-key join tables have no sequence and are skipped. An empty table is left
        /// alone (the <c>HAVING</c> guard yields no row, so <c>setval</c> is never called) — its sequence keeps
        /// its natural start, which matters because non-zero-based sequences reject a <c>setval</c> to 0.
        /// </summary>
        public static async Task AdvanceIdentitySequencesAsync(GameContext context, IEnumerable<Type> insertedTypes, CancellationToken cancellationToken)
        {
            foreach (var clrType in insertedTypes.Distinct())
            {
                var entityType = context.Model.FindEntityType(clrType);
                var key = entityType?.FindPrimaryKey();
                if (entityType is null || key is null || key.Properties.Count != 1)
                {
                    continue;
                }

                var keyProperty = key.Properties[0];
                var table = entityType.GetTableName();
                if (keyProperty.ValueGenerated != ValueGenerated.OnAdd || keyProperty.ClrType != typeof(int) || table is null)
                {
                    continue;
                }

                var column = keyProperty.GetColumnName(StoreObjectIdentifier.Table(table, entityType.GetSchema()));
                // HAVING COUNT(*) > 0 skips an empty table (setval is never called), so the next id after a
                // seeded max is max+1 (is_called defaults true) and an empty table keeps its natural start.
                var sql =
                    $"SELECT setval(pg_get_serial_sequence('\"{table}\"', '{column}'), MAX(\"{column}\")) " +
                    $"FROM \"{table}\" HAVING COUNT(*) > 0";
                await context.Database.ExecuteSqlRawAsync(sql, [], cancellationToken);
            }
        }
    }
}
