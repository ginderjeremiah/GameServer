using DataAccess.Models;
using System.Data;
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

        protected DataSet FillSet(string selectCommandText, params SqlParameter[] sqlParameters)
        {
            DataSet ds = new();
            using var connection = new SqlConnection(ConnectionString);
            var command = new SqlCommand(selectCommandText, connection);
            command.Parameters.AddRange(sqlParameters);
            var adapter = new SqlDataAdapter(command);
            adapter.Fill(ds);
            return ds;
        }

        protected DataTable FillTable(string selectCommandText, params SqlParameter[] sqlParameters)
        {
            DataTable dt = new();
            using var connection = new SqlConnection(ConnectionString);
            var command = new SqlCommand(selectCommandText, connection);
            command.Parameters.AddRange(sqlParameters);
            var adapter = new SqlDataAdapter(command);
            adapter.Fill(dt);
            return dt;
        }

        internal static DataTable FillTable<T>(IEnumerable<T> objList)
        {
            DataTable dt = new();
            var props = typeof(T).GetProperties();
            dt.Reset();
            foreach (var prop in props)
            {
                dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }
            foreach (var obj in objList)
            {
                var row = dt.NewRow();
                foreach (var prop in props)
                {
                    row[prop.Name] = prop.GetValue(obj);
                }
                dt.Rows.Add(row);
            }
            return dt;
        }

        protected List<T> QueryToList<T>(string commandText, params SqlParameter[] sqlParameters) where T : IDataModel, new()
        {
            using var connection = new SqlConnection(ConnectionString);
            var command = connection.CreateCommand();

            command.CommandText = commandText;
            command.CommandType = CommandType.Text;
            command.Parameters.AddRange(sqlParameters);
            connection.Open();

            var reader = command.ExecuteReader();
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
