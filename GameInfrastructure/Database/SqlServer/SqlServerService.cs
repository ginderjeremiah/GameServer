using GameCore.Entities;
using GameCore.Infrastructure;

namespace GameInfrastructure.Database.SqlServer
{
    internal class SqlServerService : IDatabaseService
    {
        private readonly string _connectionString;
        public SqlServerService(IDatabaseConfiguration config)
        {
            _connectionString = config.DbConnectionString;
        }

        public void Execute(Action<IDatabaseExecutor> action)
        {
            using var executor = new SqlServerExecutor(_connectionString);
            action(executor);
        }

        public T Execute<T>(Func<IDatabaseExecutor, T> func)
        {
            using var executor = new SqlServerExecutor(_connectionString);
            return func(executor);
        }

        public List<T> QueryToList<T>(string commandText, params QueryParameter[] parameters) where T : IEntity, new()
        {
            using var executor = new SqlServerExecutor(_connectionString);
            return executor.QueryToList<T>(commandText, parameters);
        }

        public (List<T1>, List<T2>) QueryToList<T1, T2>(string commandText, params QueryParameter[] parameters) where T1 : IEntity, new() where T2 : IEntity, new()
        {
            using var executor = new SqlServerExecutor(_connectionString);
            return executor.QueryToList<T1, T2>(commandText, parameters);
        }

        public (List<T1>, List<T2>, List<T3>) QueryToList<T1, T2, T3>(string commandText, params QueryParameter[] parameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
        {
            using var executor = new SqlServerExecutor(_connectionString);
            return executor.QueryToList<T1, T2, T3>(commandText, parameters);
        }

        public (List<T1>, List<T2>, List<T3>, List<T4>) QueryToList<T1, T2, T3, T4>(string commandText, params QueryParameter[] parameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
            where T4 : IEntity, new()
        {
            using var executor = new SqlServerExecutor(_connectionString);
            return executor.QueryToList<T1, T2, T3, T4>(commandText, parameters);
        }

        public void ExecuteNonQuery(string commandText, params QueryParameter[] parameters)
        {
            using var executor = new SqlServerExecutor(_connectionString);
            executor.ExecuteNonQuery(commandText, parameters);
        }

        public T ExecuteScalar<T>(string commandText, params QueryParameter[] parameters)
        {
            using var executor = new SqlServerExecutor(_connectionString);
            return executor.ExecuteScalar<T>(commandText, parameters);
        }
    }
}
