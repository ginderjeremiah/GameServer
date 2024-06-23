using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using System.Data;

namespace GameInfrastructure.Database.SqlServer
{
    internal class SqlServerExecutor : IDatabaseExecutor
    {
        private readonly SqlConnection _connection;

        public SqlServerExecutor(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
        }

        public List<T> QueryToList<T>(string commandText, params QueryParameter[] parameters) where T : IEntity, new()
        {
            var reader = GetCommand(commandText, parameters).ExecuteReader();
            return LoadFromReader<T>(reader);
        }

        public (List<T1>, List<T2>) QueryToList<T1, T2>(string commandText, params QueryParameter[] parameters) where T1 : IEntity, new() where T2 : IEntity, new()
        {
            var reader = GetCommand(commandText, parameters).ExecuteReader();
            var first = LoadFromReader<T1>(reader);
            var second = reader.NextResult() ? LoadFromReader<T2>(reader) : new List<T2>();
            return (first, second);
        }

        public (List<T1>, List<T2>, List<T3>) QueryToList<T1, T2, T3>(string commandText, params QueryParameter[] parameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
        {
            var reader = GetCommand(commandText, parameters).ExecuteReader();
            var first = LoadFromReader<T1>(reader);
            var second = reader.NextResult() ? LoadFromReader<T2>(reader) : new();
            var third = reader.NextResult() ? LoadFromReader<T3>(reader) : new();
            return (first, second, third);
        }

        public (List<T1>, List<T2>, List<T3>, List<T4>) QueryToList<T1, T2, T3, T4>(string commandText, params QueryParameter[] parameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
            where T4 : IEntity, new()
        {
            var reader = GetCommand(commandText, parameters).ExecuteReader();
            var first = LoadFromReader<T1>(reader);
            var second = reader.NextResult() ? LoadFromReader<T2>(reader) : new();
            var third = reader.NextResult() ? LoadFromReader<T3>(reader) : new();
            var fourth = reader.NextResult() ? LoadFromReader<T4>(reader) : new();
            return (first, second, third, fourth);
        }

        public void ExecuteNonQuery(string commandText, params QueryParameter[] parameters)
        {
            var command = GetCommand(commandText, parameters);
            command.ExecuteNonQuery();
        }

        public T ExecuteScalar<T>(string commandText, params QueryParameter[] parameters)
        {
            var command = GetCommand(commandText, parameters);
            return (T)command.ExecuteScalar();
        }

        public void Dispose()
        {
            _connection.Dispose();
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

        private static SqlParameter[] ConvertParameters(QueryParameter[] parameters)
        {
            return parameters.Select(GetSqlParameter).ToArray();
        }

        private SqlCommand GetCommand(string commandText, params QueryParameter[] parameters)
        {
            var sqlParameters = ConvertParameters(parameters);
            var command = new SqlCommand(commandText, _connection);
            command.Parameters.AddRange(sqlParameters);
            return command;
        }

        private static SqlParameter GetSqlParameter(QueryParameter parameter)
        {
            if (parameter.Type == null)
            {
                return new SqlParameter(parameter.ParameterName, parameter.Value)
                {
                    Direction = parameter.Direction,
                };
            }
            else if (parameter.Type != DbType.Object)
            {
                return new SqlParameter(parameter.ParameterName, parameter.Type)
                {
                    Value = parameter.Value,
                    Direction = parameter.Direction,
                };
            }
            else
            {
                var data = ((StructuredData?)parameter.Value) ?? throw new InvalidOperationException("Failed to retrieve structured data from DbType.Object parameter.");
                var metaData = new SqlMetaData[data.Columns.Count];

                for (int i = 0; i < data.Columns.Count; i++)
                {
                    //use temp parameter to convert DbType to SqlDbType
                    var tempParameter = new SqlParameter("temp", null) { DbType = data.Columns[i].Item2 };
                    metaData[i] = new SqlMetaData(data.Columns[i].Item1, tempParameter.SqlDbType);
                }

                var dataRecords = data.Rows.Select(row =>
                {
                    var record = new SqlDataRecord(metaData);

                    for (int i = 0; i < data.Columns.Count; i++)
                    {
                        record.SetValue(i, row[i]);
                    }

                    return record;
                }).ToArray();

                return new SqlParameter(parameter.ParameterName, SqlDbType.Structured)
                {
                    Value = dataRecords.Length > 0 ? dataRecords : null,
                    TypeName = data.TypeName
                };
            }
        }
    }
}
