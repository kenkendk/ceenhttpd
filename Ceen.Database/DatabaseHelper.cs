using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Ceen.Database
{
    /// <summary>
    /// Class for accessing a database, with simple table create/delete commands.
    /// Approaching a mini ORM, but omits the query parts
    /// </summary>
    public static class DatabaseHelper
    {
        /// <summary>
        /// Attempts to get a working database connection
        /// </summary>
        public static IDbConnection CreateConnection(string connectionstring, string connectionclass = "sqlite")
        {
            var classname = connectionclass;
            var connstr = connectionstring;
            if (string.Equals(classname, "sqlite", StringComparison.OrdinalIgnoreCase) || string.Equals(classname, "sqlite3", StringComparison.OrdinalIgnoreCase))
            {
                classname = "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756";
                if (Type.GetType(classname) == null)
                    classname = "System.Data.SQLite.SQLiteConnection, System.Data.SQLite, Version=1.0.104.0, Culture=neutral, PublicKeyToken=db937bc2d44ff139";

                if (!string.IsNullOrWhiteSpace(connstr) && !connstr.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                    connstr = "Data Source=" + connstr;
            }
            else if (string.Equals(classname, "odbc", StringComparison.OrdinalIgnoreCase))
                classname = "System.Data.Odbc.OdbcConnection, System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

            var contype = Type.GetType(classname);
            if (contype == null)
                throw new Exception($"Failed to locate the requested database type: {classname}");
            var e = Activator.CreateInstance(contype);
            if (!(e is IDbConnection))
                throw new Exception($"The requested type {contype.FullName} is not implementing {typeof(IDbConnection).FullName}");

            var connection = e as IDbConnection;

            connection.ConnectionString = connstr;
            connection.Open();

            return connection;
        }

        /// <summary>
        /// Gets or sets the default dialect to use
        /// </summary>
        public static IDatabaseDialect DefaultDialect = new DatabaseDialectSQLite();

        /// <summary>
        /// Gets the dialect assigned to a connection
        /// </summary>
        private static readonly Dictionary<IDbConnection, IDatabaseDialect> _dialect = new Dictionary<IDbConnection, IDatabaseDialect>();

        /// <summary>
        /// The lock used to guard the dialect table
        /// </summary>
        private static readonly object _dialectLock = new object();

        /// <summary>
        /// Gets the database dialect for a connection
        /// </summary>
        /// <returns>The dialect for the connection.</returns>
        /// <param name="connection">The connection to get the dialect for.</param>
        /// <param name="defaultOverride">A dialect that is selected instead of the default.</param>
        public static IDatabaseDialect GetDialect(this IDbConnection connection, IDatabaseDialect defaultOverride = null)
        {
            lock (_dialectLock)
            {
                if (!_dialect.TryGetValue(connection, out var res))
                    _dialect.Add(connection, res = defaultOverride ?? DefaultDialect);

                return res;
            }
        }

        /// <summary>
        /// Creates a table for the given type
        /// </summary>
        /// <typeparam name="T">The type of the table to create.</typeparam>
        /// <param name="connection">The connection to use</param>
        /// <param name="ifNotExists">Only create the table if it does not exist</param>
        public static void CreateTable<T>(this IDbConnection connection, bool ifNotExists = true)
        {
            CreateTable(connection, typeof(T), ifNotExists);
        }

        /// <summary>
        /// Creates a table for the given type
        /// </summary>
        /// <param name="type">The type of the table to create.</param>
        /// <param name="connection">The connection to use</param>
        /// <param name="ifNotExists">Only create the table if it does not exist</param>
        public static void CreateTable(this IDbConnection connection, Type type, bool ifNotExists = true)
        {
            var dialect = GetDialect(connection);
            var sql = dialect.CreateTableSql(type, ifNotExists);
            using (var cmd = connection.CreateCommand(sql))
                cmd.ExecuteNonQuery();
        }


        /// <summary>
        /// Checks if the table for the given type exists
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <typeparam name="T">The table type parameter.</typeparam>
        public static bool TableExists<T>(this IDbConnection connection)
        {
            return TableExists(connection, typeof(T));
        }

        /// <summary>
        /// Checks if the table for the given type exists
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="type">The table type parameter</param>
        public static bool TableExists(this IDbConnection connection, Type type)
        {
            var dialect = GetDialect(connection);
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = dialect.CreateTableExistsCommand(type);
                var res = cmd.ExecuteScalar();
                return res != null && res != DBNull.Value;
            }
        }

        /// <summary>
        /// Selects the item with the give ID. The type should have exactly one primary key when using this method
        /// </summary>
        /// <returns>The item by identifier.</returns>
        /// <param name="ids">The primary keys.</param>
        /// <param name="connection">The connection to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectItemById<T>(this IDbConnection connection, params object[] ids)
            where T : new()
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(typeof(T));

            if (ids == null || ids.Length != mapping.PrimaryKeys.Length)
                throw new ArgumentException($"Expected {mapping.PrimaryKeys.Length} keys but got {ids?.Length}");

            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T)) + $" WHERE " + string.Join(" AND ", mapping.PrimaryKeys.Select(x => $"{dialect.QuoteName(x.Name)} = ?")) + " LIMIT 1"))
            using(var rd = cmd.ExecuteReader(ids))
            {
                return rd.NextResult() 
                    ? FillItem(rd, mapping, new T())
                    : default(T);
            }
        }

        /// <summary>
        /// Gets all items for a given type
        /// </summary>
        /// <returns>All items of the given type.</returns>
        /// <param name="connection">The connection to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> SelectAll<T>(this IDbConnection connection)
            where T : new()
        {
            var dialect = GetDialect(connection);
            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T))))
                foreach (var n in FillItems<T>(cmd))
                    yield return n;
        }

        /// <summary>
        /// Gets some items for a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The where part of the query to use</param>
        /// <param name="arguments">The query arguments</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> Select<T>(this IDbConnection connection, string query, params object[] arguments)
            where T : new()
        {
            var dialect = GetDialect(connection);
            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T)) + query))
            {
                cmd.SetParameterValues(arguments);
                foreach (var n in FillItems<T>(cmd))
                    yield return n;
            }
        }

        /// <summary>
        /// Gets a single item of a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The where part of the query to use</param>
        /// <param name="arguments">The query arguments</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectSingle<T>(this IDbConnection connection, string query, params object[] arguments)
            where T : new()
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(typeof(T));
            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T)) + query + " LIMIT 1"))
            using(var rd = cmd.ExecuteReader(arguments))
            {
                return rd.NextResult()
                    ? FillItem(rd, mapping, new T())
                    : default(T);
            }
        }

        /// <summary>
        /// Executes a non-query
        /// </summary>
        /// <returns>The number of records affected.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        public static int ExecuteNonQuery(this IDbConnection connection, string query, params object[] arguments)
        {
            var dialect = GetDialect(connection);
            using (var cmd = connection.CreateCommandWithParameters(query))
                return cmd.ExecuteNonQuery(arguments);
        }

        /// <summary>
        /// Performs a database query and returns the results.
        /// Note this function requires the entire SQL command to be given as the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <returns>The items that match.</returns>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> Query<T>(this IDbConnection connection, string query, params object[] arguments)
            where T : new()
        {
            using (var cmd = connection.CreateCommandWithParameters(query))
            {
                cmd.SetParameterValues(arguments);
                foreach (var n in FillItems<T>(cmd))
                    yield return n;
            }
        }

        /// <summary>
        /// Performs a database query and returns the first result.
        /// Note this function requires the entire SQL command to be given and should have &quot;LIMIT 1&quot; at the end.
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <returns>The items that match.</returns>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T QuerySingle<T>(this IDbConnection connection, string query, params object[] arguments)
            where T : new()
        {
            using (var cmd = connection.CreateCommandWithParameters(query))
            using (var rd = cmd.ExecuteReader(arguments))
                if (rd.NextResult())
                    return FillItem(rd, GetDialect(connection).GetTypeMap(typeof(T)), new T());

            return default(T);
        }

        /// <summary>
        /// Fills the given item with properties from the query
        /// </summary>
        /// <returns>The items.</returns>
        /// <param name="command">The command to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> FillItems<T>(this IDbCommand command)
            where T : new()
        {
            var dialect = GetDialect(command.Connection);
            var mapping = dialect.GetTypeMap(typeof(T));

            using(var reader = command.ExecuteReader())
                while (reader.NextResult())
                    yield return FillItem(reader, mapping, new T());
        }

        /// <summary>
        /// Fills the given item with properties from the query
        /// </summary>
        /// <returns>The item.</returns>
        /// <param name="reader">The reader to use</param>
        /// <param name="item">The item to fill.</param>
        /// <param name="map">The table mapping to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T FillItem<T>(this IDataReader reader, TableMapping map, T item)
        {
            var props = map.AllColumnsBySqlName;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (props.TryGetValue(reader.GetName(i), out var prop))
                {
                    var value = reader.GetValue(i);
                    if (value == DBNull.Value)
                        value = null;

                    if (value == null && prop.Property.PropertyType.IsValueType)
                        value = Activator.CreateInstance(prop.Property.PropertyType);

                    prop.Property.SetValue(item, value);
                }
            }

            return item;
        }

        /// <summary>
        /// Updates the item, using the primary key to locate it.
        /// </summary>
        /// <returns><c>true</c>, if item was updated, <c>false</c> otherwise.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item to update.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static bool UpdateItem<T>(this IDbConnection connection, T item)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(typeof(T));

            var txt = 
                $"UPDATE {dialect.QuoteName(mapping.Name)}"
                + $" SET {string.Join(", ", mapping.ColumnsWithoutPrimaryKey.Select(x => $"{dialect.QuoteName(x.Name)} = ?" ))}"
                + $" WHERE " + string.Join(" AND ", mapping.PrimaryKeys.Select(x => $"{dialect.QuoteName(x.Name)} = ?"))
                + " LIMIT 1";

            using (var cmd = connection.CreateCommandWithParameters(txt))
                return cmd.ExecuteNonQuery(mapping.ColumnsWithoutPrimaryKey.Concat(mapping.PrimaryKeys).Select(x => x.Property.GetValue(item)).ToArray()) > 0;
        }

        /// <summary>
        /// Deletes items by using the primary key of the given item
        /// </summary>
        /// <returns><c>true</c>, if item was deleted, <c>false</c> otherwise.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item to get the primary key from.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static bool DeleteItem<T>(this IDbConnection connection, T item)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(typeof(T));
            return DeleteItemById(connection, typeof(T), mapping.PrimaryKeys.Select(x => x.Property.GetValue(item)).ToArray());
        }

        /// <summary>
        /// Deletes the item with the given ID
        /// </summary>
        /// <returns><c>true</c>, if item was deleted, <c>false</c> otherwise.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="ids">The primary key.</param>
        /// <param name="tdata">The data type</param>
        public static bool DeleteItemById(this IDbConnection connection, Type tdata, params object[] ids)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(tdata);
            if (ids == null || ids.Length != mapping.PrimaryKeys.Length)
                throw new ArgumentException($"Expected {mapping.PrimaryKeys.Length} keys but got {ids?.Length}");

            var txt = $"DELETE FROM {dialect.QuoteName(mapping.Name)} " + $" WHERE " + string.Join(" AND ", mapping.PrimaryKeys.Select(x => $"{dialect.QuoteName(x.Name)} = ?")) + " LIMIT 1";
            using (var cmd = connection.CreateCommandWithParameters(txt))
                return cmd.ExecuteNonQuery(ids) > 0;
        }

        /// <summary>
        /// Deletes the item with the given ID
        /// </summary>
        /// <returns><c>true</c>, if item was deleted, <c>false</c> otherwise.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="ids">The primary key.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static bool DeleteItemById<T>(this IDbConnection connection, params object[] ids)
        {
            return DeleteItemById(connection, typeof(T), ids);
        }

        /// <summary>
        /// Inserts the given item into the database
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item to insert.</param>
        /// <returns><c>true</c>, if item inserted, <c>false</c> otherwise.</returns>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static bool InsertItem<T>(this IDbConnection connection, T item)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(typeof(T));

            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateInsertCommand(typeof(T))))
                return cmd.ExecuteNonQuery(mapping.AllColumns.Select(x => x.Property.GetValue(item))) > 0;
        }
    }
}
