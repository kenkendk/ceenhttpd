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
        /// <param name="autoAddColumns">Automatically add new columns to the database if they are not present</param>
        public static void CreateTable<T>(this IDbConnection connection, bool ifNotExists = true, bool autoAddColumns = true)
        {
            CreateTable(connection, typeof(T), ifNotExists, autoAddColumns);
        }

        /// <summary>
        /// Creates a table for the given type
        /// </summary>
        /// <param name="type">The type of the table to create.</param>
        /// <param name="connection">The connection to use</param>
        /// <param name="ifNotExists">Only create the table if it does not exist</param>
        /// <param name="autoAddColumns">Automatically add new columns to the database if they are not present</param>
        public static void CreateTable(this IDbConnection connection, Type type, bool ifNotExists = true, bool autoAddColumns = true)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(type);

            var sql = dialect.CreateTableSql(type, ifNotExists);
            using (var cmd = connection.CreateCommand(sql))
                cmd.ExecuteNonQuery();

            sql = dialect.CreateSelectTableColumnsSql(type);
            var columns = new List<string>();
            using (var cmd = connection.CreateCommand(sql))
            using(var rd = cmd.ExecuteReader())
                while(rd.Read())
                    columns.Add(rd.GetAsString(0));

            var missingcolumns = mapping.AllColumns.Where(x => !columns.Contains(x.ColumnName)).ToArray();
            if (missingcolumns.Length != 0)
            {
                if (!autoAddColumns)
                    throw new DataException($"The table {mapping.Name} is missing the column(s): {string.Join(", ", missingcolumns.Select(x => x.ColumnName))}");

                sql = dialect.CreateAddColumnSql(type, missingcolumns);
                using (var cmd = connection.CreateCommand(sql))
                    cmd.ExecuteNonQuery();
            }
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

            if (mapping.PrimaryKeys.Length == 0)
                throw new ArgumentException("Cannot select by ID when there are no primary keys on the table");

            if (ids == null || ids.Length != mapping.PrimaryKeys.Length)
                throw new ArgumentException($"Expected {mapping.PrimaryKeys.Length} keys but got {ids?.Length}");

            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T)) + $" WHERE " + string.Join(" AND ", mapping.PrimaryKeys.Select(x => $"{dialect.QuoteName(x.ColumnName)} = ?")) + " LIMIT 1"))
            using(var rd = cmd.ExecuteReader(ids))
            {
                return rd.Read() 
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
        /// Adds a space in from of the query string
        /// </summary>
        /// <param name="query">The query string</param>
        /// <returns>A space prefixed query string</returns>
        private static string SpacePrefixQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;
            return " " + query.Trim();
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
            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T)) + SpacePrefixQuery(query)))
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
            using (var cmd = connection.CreateCommandWithParameters(dialect.CreateSelectCommand(typeof(T)) + SpacePrefixQuery(query) + " LIMIT 1"))
            using(var rd = cmd.ExecuteReader(arguments))
            {
                return rd.Read()
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
        /// Executes a query and returns the scalar value
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <returns>The scalar item</returns>
        public static object ExecuteScalar(this IDbConnection connection, string query, params object[] arguments)
        {
            var dialect = GetDialect(connection);
            using (var cmd = connection.CreateCommandWithParameters(query))
            {
                cmd.SetParameterValues(arguments);
                using(var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                        return null;

                    // Fix issue with long conversion
                    if (rd.GetFieldType(0) == typeof(long) || rd.GetFieldType(0) == typeof(int))
                        return rd.GetInt64(0);

                    var res = rd.GetValue(0);                       
                    if (res == DBNull.Value)
                        return null;

                    return res;
                }
            }
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
                if (rd.Read())
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
                while (reader.Read())
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

                    if (value == null && prop.MemberType.IsValueType)
                        value = Activator.CreateInstance(prop.MemberType);

                    prop.SetValueFromDb(item, value);
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

            if (mapping.PrimaryKeys.Length == 0)
                throw new ArgumentException("Cannot perform update there are no primary keys on the table");

            var txt = 
                $"UPDATE {dialect.QuoteName(mapping.Name)}"
                + $" SET {string.Join(", ", mapping.UpdateColumns.Select(x => $"{dialect.QuoteName(x.ColumnName)} = ?" ))}"
                + $" WHERE " + string.Join(" AND ", mapping.PrimaryKeys.Select(x => $"{dialect.QuoteName(x.ColumnName)} = ?"));

            using (var cmd = connection.CreateCommandWithParameters(txt))
                return cmd.ExecuteNonQuery(mapping.UpdateColumns.Select(x => {
                    switch (x.AutoGenerateAction)
                    {
                        case AutoGenerateAction.ClientChangeTimestamp:
                            return DateTime.Now;
                        default:
                            return x.GetValueForDb(item);
                    }                    
                }).Concat(mapping.PrimaryKeys.Select(x => x.GetValueForDb(item)))) > 0;
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
            return DeleteItemById(connection, typeof(T), mapping.PrimaryKeys.Select(x => x.GetValueForDb(item)).ToArray());
        }

        /// <summary>
        /// Deletes the item with the given ID
        /// </summary>
        /// <returns><c>true</c>, if item was deleted, <c>false</c> otherwise.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="ids">The primary key.</param>
        /// <param name="tdata">The data type</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static bool DeleteItemById(this IDbConnection connection, Type tdata, params object[] ids)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(tdata);
            if (mapping.PrimaryKeys.Length == 0)
                throw new ArgumentException("Cannot perform delete by ID when there are no primary keys on the table");
            if (ids == null || ids.Length != mapping.PrimaryKeys.Length)
                throw new ArgumentException($"Expected {mapping.PrimaryKeys.Length} keys but got {ids?.Length}");


            var txt = dialect.CreateDeleteByIdCommand(tdata);
            using (var cmd = connection.CreateCommandWithParameters(txt))
                return cmd.ExecuteNonQuery(ids) > 0;
        }

        /// <summary>
        /// Deletes items matching the where clause
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="whereclause">The where clause</param>
        /// <param name="arguments">The arguments for the where clause</param>
        /// <returns>The number of items</returns>
        public static int Delete<T>(this IDbConnection connection, string whereclause, params object[] arguments)
        {
            return Delete(connection, typeof(T), whereclause, arguments);
        }

        /// <summary>
        /// Deletes items matching the where clause
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="tdata">The data type to use</param>
        /// <param name="whereclause">The where clause</param>
        /// <param name="arguments">The arguments for the where clause</param>
        /// <returns>The number of items</returns>
        public static int Delete(this IDbConnection connection, Type tdata, string whereclause, params object[] arguments)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(tdata);

            var txt = dialect.CreateDeleteCommand(tdata);
            using(var cmd = connection.CreateCommandWithParameters(txt + SpacePrefixQuery(whereclause)))
                return cmd.ExecuteNonQuery(arguments);
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
            {
                // TODO: We might want to read the value from the database
                // instead of relying on the generated value as we might
                // have precision loss for the timestamps when going to the DB and back,
                // which would result in value returned from create and subsequent select
                // operations to differ
                var clientgenerated = new Dictionary<ColumnMapping, object>();

                var arguments = mapping.InsertColumns.Select(x =>
                {
                    switch (x.AutoGenerateAction)
                    {
                        case AutoGenerateAction.ClientCreateTimestamp:
                        case AutoGenerateAction.ClientChangeTimestamp:
                            return clientgenerated[x] = DateTime.Now; 
                        case AutoGenerateAction.ClientGenerateGuid:
                            return clientgenerated[x] = Guid.NewGuid().ToString();
                    }

                    return x.GetValueForDb(item);
                });

                if (!mapping.IsPrimaryKeyAutogenerated) {
                    var res = cmd.ExecuteNonQuery(arguments) > 0;
                    foreach (var x in clientgenerated)
                        x.Key.SetValue(item, x.Value);
                    return res;
                }

                var id = cmd.ExecuteScalar(arguments);
                var pk = mapping.PrimaryKeys.First();
                pk.SetValueFromDb(item, id);
                foreach (var x in clientgenerated)
                    x.Key.SetValue(item, x.Value);

                return true;
            }
        }
    }
}
