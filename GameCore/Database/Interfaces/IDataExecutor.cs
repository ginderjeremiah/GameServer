namespace GameCore.Database.Interfaces
{
    public interface IDataExecutor : IDisposable
    {
        public List<T> QueryToList<T>(string commandText, params QueryParameter[] parameters) where T : IEntity, new();

        public (List<T1>, List<T2>) QueryToList<T1, T2>(string commandText, params QueryParameter[] parameters) where T1 : IEntity, new() where T2 : IEntity, new();

        public (List<T1>, List<T2>, List<T3>) QueryToList<T1, T2, T3>(string commandText, params QueryParameter[] parameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new();

        public (List<T1>, List<T2>, List<T3>, List<T4>) QueryToList<T1, T2, T3, T4>(string commandText, params QueryParameter[] parameters)
            where T1 : IEntity, new()
            where T2 : IEntity, new()
            where T3 : IEntity, new()
            where T4 : IEntity, new();

        public void ExecuteNonQuery(string commandText, params QueryParameter[] parameters);

        public T ExecuteScalar<T>(string commandText, params QueryParameter[] parameters);
    }
}
