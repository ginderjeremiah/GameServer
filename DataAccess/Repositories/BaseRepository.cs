using DataAccess.Entities;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class BaseRepository
    {
        protected string ConnectionString { get; set; }

        protected BaseRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        protected List<T> QueryToList<T>(string commandText, params SqlParameter[] sqlParameters) where T : IEntity, new()
        {
            using var connection = new SqlConnection(ConnectionString);
            return LoadFromReader<T>(connection.GetReader(commandText, sqlParameters));
        }

        protected (List<T1>, List<T2>) QueryToList<T1, T2>(string commandText, params SqlParameter[] sqlParameters) where T1 : IEntity, new() where T2 : IEntity, new()
        {
            using var connection = new SqlConnection(ConnectionString);
            var reader = connection.GetReader(commandText, sqlParameters);
            var first = LoadFromReader<T1>(reader);
            var second = reader.NextResult() ? LoadFromReader<T2>(reader) : new List<T2>();
            return (first, second);
        }

        protected (List<T1>, List<T2>, List<T3>) QueryToList<T1, T2, T3>(string commandText, params SqlParameter[] sqlParameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
        {
            using var connection = new SqlConnection(ConnectionString);
            var reader = connection.GetReader(commandText, sqlParameters);
            var first = LoadFromReader<T1>(reader);
            var second = reader.NextResult() ? LoadFromReader<T2>(reader) : new();
            var third = reader.NextResult() ? LoadFromReader<T3>(reader) : new();
            return (first, second, third);
        }

        protected (List<T1>, List<T2>, List<T3>, List<T4>) QueryToList<T1, T2, T3, T4>(string commandText, params SqlParameter[] sqlParameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
            where T4 : IEntity, new()
        {
            using var connection = new SqlConnection(ConnectionString);
            var reader = connection.GetReader(commandText, sqlParameters);
            var first = LoadFromReader<T1>(reader);
            var second = reader.NextResult() ? LoadFromReader<T2>(reader) : new();
            var third = reader.NextResult() ? LoadFromReader<T3>(reader) : new();
            var fourth = reader.NextResult() ? LoadFromReader<T4>(reader) : new();
            return (first, second, third, fourth);
        }

        private static List<T> LoadFromReader<T>(SqlDataReader reader) where T : IEntity, new()
        {
            var data = new List<T>();

            while (reader.Read())
            {
                T obj = new();
                obj.LoadFromReader(reader);
                data.Add(obj);
            }

            return data;
        }

        protected void ExecuteNonQuery(string commandText, params SqlParameter[] sqlParameters)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            ExecuteNonQuery(commandText, connection, sqlParameters);
        }

        protected static void ExecuteNonQuery(string commandText, SqlConnection connection, params SqlParameter[] sqlParameters)
        {
            var command = new SqlCommand(commandText, connection);
            command.Parameters.AddRange(sqlParameters);
            command.ExecuteNonQuery();
        }

        protected T ExecuteScalar<T>(string commandText, params SqlParameter[] sqlParameters)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return ExecuteScalar<T>(commandText, connection, sqlParameters);
        }

        protected static T ExecuteScalar<T>(string commandText, SqlConnection connection, params SqlParameter[] sqlParameters)
        {
            var command = new SqlCommand(commandText, connection);
            command.Parameters.AddRange(sqlParameters);
            return (T)command.ExecuteScalar();
        }
    }
}
