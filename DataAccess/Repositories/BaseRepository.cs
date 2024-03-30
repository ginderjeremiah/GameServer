using DataAccess.Models;
using System.Data;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class BaseRepository
    {
        //private static readonly ConcurrentDictionary<Type, Dictionary<string, Action<object, object>>> _propertySetters = new();
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

        protected List<T> QueryToList<T>(string commandText, params SqlParameter[] sqlParameters) where T : IModel, new()
        {
            using var connection = new SqlConnection(ConnectionString);
            var command = connection.CreateCommand();

            command.CommandText = commandText;
            command.CommandType = CommandType.Text;
            command.Parameters.AddRange(sqlParameters);
            connection.Open();

            var reader = command.ExecuteReader();
            var props = typeof(T).GetProperties();
            //var props = GetPropertySetters(typeof(T));
            var objs = new List<T>();

            while (reader.Read())
            {
                T obj = new();
                obj.LoadFromReader(reader);
                //foreach (var prop in props)
                //{
                //    switch (reader.GetValue(prop.Name))
                //    {
                //        case DBNull:
                //            prop.SetValue(obj, null);
                //            break;
                //        case object val:
                //            prop.SetValue(obj, val);
                //            break;
                //    }
                //}
                //for (int i = 0; i < reader.FieldCount; i++)
                //{
                //    var colName = reader.GetName(i);
                //    props[colName](obj, reader.GetValue(i));
                //}
                objs.Add(obj);
            }
            return objs;
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

        //private static Dictionary<string, Action<object, object>> GetPropertySetters(Type t)
        //{
        //    if (_propertySetters.TryGetValue(t, out var propertySetters))
        //    {
        //        return propertySetters;
        //    }

        //    return InitPropertySetters(t);
        //}

        //private static Dictionary<string, Action<object, object>> InitPropertySetters(Type t)
        //{
        //    var propertySetters = new Dictionary<string, Action<object, object>>();
        //    foreach (var property in t.GetProperties())
        //    {
        //        propertySetters[property.Name] = (Action<object, object>)Delegate.CreateDelegate(typeof(Action<object, object>), property.GetSetMethod());
        //    }
        //    _propertySetters[t] = propertySetters;
        //    return propertySetters;
        //}
    }
}
