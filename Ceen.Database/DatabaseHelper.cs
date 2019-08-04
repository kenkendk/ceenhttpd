using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
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
        /// Parses a filterstring
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <param name="filter">The filter to parse</param>
        /// <typeparam name="T">The type the query is for</typeparam>
        /// <returns>The filter query</returns>
        public static QueryElement ParseFilter<T>(this IDbConnection connection, string filter)
        {
            return GetDialect(connection).ParseFilter<T>(filter);
        }

        /// <summary>
        /// Renders query using the current dialect
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <param name="query">The query to render</param>
        /// <typeparam name="T">The type the query is for</typeparam>
        /// <returns>The query and arguments</returns>
        public static KeyValuePair<string, object[]> RenderWhereClause<T>(this IDbConnection connection, QueryElement query)
        {
            return GetDialect(connection).RenderWhereClause(typeof(T), query);
        }

        /// <summary>
        /// Renders query using the current dialect
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <param name="query">The query to render</param>
        /// <typeparam name="T">The type the query is for</typeparam>
        /// <returns>The query and arguments</returns>
        public static KeyValuePair<string, object[]> RenderWhereClause<T>(this IDbConnection connection, Expression<Func<T, bool>> query)
        {
            return GetDialect(connection).RenderWhereClause(typeof(T), QueryUtil.FromLambda(query));
        }

        /// <summary>
        /// Renders query using the current dialect
        /// </summary>
        /// <param name="connection">The dialect to use</param>
        /// <param name="query">The query to render</param>
        /// <typeparam name="T">The type the query is for</typeparam>
        /// <returns>The query and arguments</returns>
        public static KeyValuePair<string, object[]> RenderWhereClause<T>(this IDatabaseDialect dialect, Expression<Func<T, bool>> query)
        {
            return dialect.RenderWhereClause(typeof(T), QueryUtil.FromLambda(query));
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
            return SelectSingle<T>(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .MatchPrimaryKeys(ids)
                    .Limit(1)
            );
        }

        /// <summary>
        /// Selects a single item
        /// </summary>
        /// <returns>The first item matching the query.</returns>
        /// <param name="query">The query to use.</param>
        /// <param name="connection">The connection to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectSingle<T>(this IDbConnection connection, Query query)
            where T : new()
        {
            return SelectSingle<T>(connection, query, () => new T());
        }
        /// <summary>
        /// Selects a single item
        /// </summary>
        /// <returns>The first item matching the query.</returns>
        /// <param name="query">The query to use.</param>
        /// <param name="connection">The connection to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectSingle<T>(this IDbConnection connection, Query<T> query)
            where T : new()
        {
            return SelectSingle<T>(connection, query, () => new T());
        }

        /// <summary>
        /// Selects a single item
        /// </summary>
        /// <returns>The first item matching the query.</returns>
        /// <param name="query">The query to use.</param>
        /// <param name="connection">The connection to use</param>
        /// <param name="create">The method creating the instance of <typeref name="T" /> to update</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectSingle<T>(this IDbConnection connection, Query query, Func<T> create)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap<T>();

            if (query.Parsed.Type != QueryType.Select)
                throw new ArgumentException($"Cannot select with a query of type {query.Parsed.Type}");

            // Force a limit of one result
            if (query.Parsed.LimitParams == null || query.Parsed.LimitParams.Item1 < 0)
                query.Parsed.Limit(1);
            else if (query.Parsed.LimitParams.Item1 != 1)
                throw new ArgumentException("The limit cannot be set when selecting a single item");

            var q = dialect.RenderStatement(query);
            using (var cmd = connection.CreateCommandWithParameters(q.Key))
            using(var rd = cmd.ExecuteReader(q.Value))
            {
                return rd.Read() 
                    ? FillItem(rd, mapping, create())
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
            return Select(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
            );
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
        /// <param name="query">The query to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> Select<T>(this IDbConnection connection, Expression<Func<T, bool>> query)
            where T : new()
        {
            return Select(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .Where(query)
            );
        }

        /// <summary>
        /// Gets some items for a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> Select<T>(this IDbConnection connection, QueryElement query)
            where T : new()
        {
            return Select(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .Where(query)
            );
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
            return Select(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .Where(new CustomQuery(query, arguments))
            );
        }

        /// <summary>
        /// Gets some items for a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="query">The query to use</param>
        /// <param name="arguments">The query arguments</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> Select<T>(this IDbConnection connection, Query<T> query)
            where T : new()
        {
            return Select<T>(connection, (Query)query);
        }

        /// <summary>
        /// Gets some items for a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The where part of the query to use</param>
        /// <param name="arguments">The query arguments</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static IEnumerable<T> Select<T>(this IDbConnection connection, Query query)
            where T : new()
        {
            var dialect = GetDialect(connection);
            var q = dialect.RenderStatement(query);

            using (var cmd = connection.CreateCommandWithParameters(q.Key))
            {
                cmd.SetParameterValues(q.Value);
                foreach (var n in FillItems<T>(cmd))
                    yield return n;
            }
        }        

        /// <summary>
        /// Gets a single item of a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectSingle<T>(this IDbConnection connection, Expression<Func<T, bool>> query)
            where T : new()
        {
            return SelectSingle<T>(
                connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .Where(query)
            );
        }

        /// <summary>
        /// Gets a single item of a given type
        /// </summary>
        /// <returns>The items that match.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static T SelectSingle<T>(this IDbConnection connection, QueryElement query)
            where T : new()
        {
            return SelectSingle<T>(
                connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .Where(query)
            );
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
            return SelectSingle<T>(
                connection,
                GetDialect(connection)
                    .Query<T>()
                    .Select()
                    .Where(new CustomQuery(query, arguments))
            );
        }

        /// <summary>
        /// Handles a number of issues with a database reader, such as DBNull values and broken int64 support
        /// </summary>
        /// <param name="reader">The reader to read from</param>
        /// <param name="index">The value to read</param>
        /// <returns>The normalized value</returns>
        public static object GetNormalizedValue(this IDataReader reader, int index)
        {
            var res = reader.GetValue(index);
            // Convert DBNull to null
            if (res == DBNull.Value)
                return null;

            // Otherwise, just return it
            return res;
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
        /// Returns the number of records that match the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The where clause to use</param>
        /// <typeparam name="T">The table type</typeparam>
        /// <returns>The count</returns>
        public static long SelectCount<T>(this IDbConnection connection, Expression<Func<T, bool>> query)
        {
            return SelectCount<T>(connection, QueryUtil.FromLambda(query));
        }

        /// <summary>
        /// Returns the number of records that match the query.
        /// Note that this method works on all query types (select, insert, update, delete)
        /// and ignores any limits set on the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to extract the where clause from</param>
        /// <typeparam name="T">The table type</typeparam>
        /// <returns>The count</returns>
        public static long SelectCount<T>(this IDbConnection connection, Query<T> query)
        {
            return SelectCount<T>(connection, query.Parsed.WhereQuery);
        }

        /// <summary>
        /// Returns the number of records that match the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The where clause to use</param>
        /// <typeparam name="T">The table type</typeparam>
        /// <returns>The count</returns>
        public static long SelectCount<T>(this IDbConnection connection, QueryElement query)
        {
            var q = connection.GetDialect().RenderWhereClause(typeof(T), query);
            return SelectCount<T>(connection, q.Key, q.Value);
        }

        /// <summary>
        /// Returns the number of records that match the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The where clause to use</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <typeparam name="T">The table type</typeparam>
        /// <returns>The count</returns>
        public static long SelectCount<T>(this IDbConnection connection, string query, params object[] arguments)
        {
            return (long)Convert.ChangeType(ExecuteScalar(connection, $"SELECT COUNT(*) FROM {connection.QuotedTableName<T>()}" + SpacePrefixQuery(query), arguments), typeof(long));
        }

        /// <summary>
        /// Executes a query and returns the scalar value
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The SQL statement to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <typeparam name="T">The data type to return as the scalar</typeparam>
        /// <returns>The scalar item</returns>
        public static T ExecuteScalar<T>(this IDbConnection connection, string query, params object[] arguments)
        {
            return (T)Convert.ChangeType(ExecuteScalar(connection, query, arguments), typeof(T));
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
                    return rd.GetNormalizedValue(0);                       
                }
            }
        }

        /// <summary>
        /// Performs a database query and returns the results.
        /// Note this function requires the entire SQL command to be given as the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <returns>A blank new query.</returns>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static Query<T> Query<T>(this IDbConnection connection)
        {
            return GetDialect(connection).Query<T>();
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
        public static IEnumerable<T> CustomQuery<T>(this IDbConnection connection, string query, params object[] arguments)
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
        public static T CustomQuerySingle<T>(this IDbConnection connection, string query, params object[] arguments)
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
                    var value = reader.GetNormalizedValue(i);
                    if (value == null && prop.MemberType.IsValueType)
                        value = Activator.CreateInstance(prop.MemberType);

                    prop.SetValueFromDb(item, value);
                }
            }

            return item;
        }

        /// <summary>
        /// Updates one or more items with the given values.
        /// </summary>
        /// <returns>The number of rows updated.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item with values to update.</param>
        /// <param name="query">The where clause to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static int Update<T>(this IDbConnection connection, object item, Expression<Func<T, bool>> query)
        {
            return Update(connection,
                GetDialect(connection).
                    Query<T>()
                    .Update(item)
                    .Where(query)
            );
        }

        /// <summary>
        /// Updates one or more items with the given values.
        /// </summary>
        /// <returns>The number of rows updated.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item with values to update.</param>
        /// <param name="query">The where clause to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static int Update<T>(this IDbConnection connection, object item, QueryElement query)
        {
            return Update(connection,
                GetDialect(connection).
                    Query<T>()
                    .Update(item)
                    .Where(query)
            );
        }

        /// <summary>
        /// Updates one or more items with the given values.
        /// </summary>
        /// <returns>The number of rows updated.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item with values to update.</param>
        /// <param name="query">The SQL where clause to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static int Update<T>(this IDbConnection connection, object item, string query, params object[] arguments)
        {
            return Update(connection,
                GetDialect(connection).
                    Query<T>()
                    .Update(item)
                    .Where(new CustomQuery(query, arguments))
            );
        }

        /// <summary>
        /// Updates one or more items with the given values.
        /// </summary>
        /// <returns>The number of rows updated.</returns>
        /// <param name="connection">The connection to use</param>
        /// <param name="values">Thev values to update.</param>
        /// <param name="query">The SQL where clause to execute</param>
        /// <param name="arguments">The arguments to the command</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        public static int Update<T>(this IDbConnection connection, Dictionary<string, object> values, string query, params object[] arguments)
        {
            return Update(connection,
                GetDialect(connection).
                    Query<T>()
                    .Update(values)
                    .Where(new CustomQuery(query, arguments))
            );
        }

        /// <summary>
        /// Performs an update using the query
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The update query</param>
        /// <returns>The number of rows updated.</returns>
        public static int Update(this IDbConnection connection, Query query)
        {
            if (query.Parsed.Type != QueryType.Update)
                throw new InvalidOperationException($"Cannot use a query of type {query.Parsed.Type} for UPDATE");

            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(query.Parsed.DataType);
            mapping.Validate(query.Parsed.UpdateValues);

            var q = connection.GetDialect().RenderStatement(query);
            using (var cmd = connection.CreateCommandWithParameters(q.Key))
                return cmd.ExecuteNonQuery(q.Value);
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
            return Update(connection,
                GetDialect(connection).
                    Query<T>()
                    .Update(item)
                    .MatchPrimaryKeys(item)
            ) > 0;
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
            return DeleteItemById<T>(connection, mapping.PrimaryKeys.Select(x => x.GetValueForDb(item)).ToArray());
        }

        /// <summary>
        /// Deletes items matching the where clause
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="tdata">The data type to use</param>
        /// <param name="query">The query to use</param>
        /// <returns>The number of items</returns>
        public static int Delete<T>(this IDbConnection connection, Expression<Func<T, bool>> query)
        {
            return Delete(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Delete()
                    .Where(query)
                );
        }

        /// <summary>
        /// Deletes items matching the where clause
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        /// <returns>The number of items</returns>
        public static int Delete<T>(this IDbConnection connection, QueryElement query)
        {
            return Delete(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Delete()
                    .Where(query)
            );
        }

        /// <summary>
        /// Deletes items matching the where clause
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="whereclause">The where clause</param>
        /// <param name="arguments">The arguments for the where clause</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        /// <returns>The number of items</returns>
        public static int Delete<T>(this IDbConnection connection, string whereclause, params object[] arguments)
        {
            return Delete(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Delete()
                    .Where(new CustomQuery(whereclause, arguments)
                )
            );
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
            var map = GetDialect(connection).GetTypeMap<T>();
            return Delete(connection,
                GetDialect(connection)
                    .Query<T>()
                    .Delete()
                    .Limit(1)
                    .MatchPrimaryKeys(ids)
            ) > 0;
        }

        /// <summary>
        /// Deletes items matching the where clause
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use</param>
        /// <returns>The number of items deleted</returns>
        public static int Delete(this IDbConnection connection, Query query)
        {
            if (query.Parsed.Type != QueryType.Delete)
                throw new InvalidOperationException($"Cannot use a query of type {query.Parsed.Type} for DELETE");
            var q = connection.GetDialect().RenderStatement(query);
            using (var cmd = connection.CreateCommandWithParameters(q.Key))
                return cmd.ExecuteNonQuery(q.Value);
        }


        /// <summary>
        /// Inserts the given item into the database
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item to insert.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        /// <returns>The inserted item</returns>
        public static T InsertOrIgnoreItem<T>(this IDbConnection connection, T item)
        {
            return Insert<T>(
                connection,
                GetDialect(connection)
                    .Query<T>()
                    .Insert(item)
                    .IgnoreInsertFailure()
            );
        }

        /// <summary>
        /// Inserts the given item into the database
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="item">The item to insert.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        /// <returns>The inserted item</returns>
        public static T InsertItem<T>(this IDbConnection connection, T item)
        {
            // Catch bad mappings from the user
            if (item is Query)
                return Insert<T>(connection, (Query)(object)item);

            return Insert<T>(
                connection,
                GetDialect(connection)
                    .Query<T>()
                    .Insert(item)
            );
        }

        /// <summary>
        /// Inserts the given item into the database
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        /// <returns>The inserted item</returns>
        public static T Insert<T>(this IDbConnection connection, Query<T> query)
        {
            return Insert<T>(connection, (Query)query);
        }

        /// <summary>
        /// Inserts the given item into the database
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="query">The query to use.</param>
        /// <typeparam name="T">The data type parameter.</typeparam>
        /// <returns>The inserted item</returns>
        public static T Insert<T>(this IDbConnection connection, Query query)
        {
            var dialect = GetDialect(connection);
            var mapping = dialect.GetTypeMap(typeof(T));

            var item = (T)query.Parsed.InsertItem;
            mapping.Validate(item);

            var q = dialect.RenderStatement(query);

            using (var cmd = connection.CreateCommandWithParameters(q.Key))
            {
                if (!mapping.IsPrimaryKeyAutogenerated)
                {
                    var res = cmd.ExecuteNonQuery(q.Value) > 0;
                    foreach (var n in mapping.PrimaryKeys)
                        n.SetValueFromDb(item, query.Parsed.UpdateValues[n.MemberName]);
                }
                else
                {
                    var id = cmd.ExecuteScalar(q.Value);
                    var pk = mapping.PrimaryKeys.First();
                    pk.SetValueFromDb(item, id);
                }
            }

            // TODO: We could also read back the client generated to ensure that the values do not
            // have small precision differences
            foreach (var x in mapping.ClientGenerated)
                if (query.Parsed.UpdateValues.TryGetValue(x.MemberName, out var v))
                    x.SetValue(item, v);

            if (mapping.DatabaseGenerated.Count > (mapping.IsPrimaryKeyAutogenerated ? 1 : 0))
            {
                // Read back the client and database generated values
                var cols = mapping.DatabaseGenerated.Concat(mapping.ClientGenerated);
                SelectSingle(
                    connection,
                    connection.Query<T>()
                        .Select(cols.Select(x => x.MemberName).ToArray())
                        .MatchPrimaryKeys(item),
                    () => item
                );
            }

            return item;
        }

        /// <summary>
        /// Gets the quoted tablename for the table
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <typeparam name="T">The type to get the tablename from</typeparam>
        /// <returns>The quoted table name</returns>
        public static string QuotedTableName<T>(this IDbConnection connection)
        {
            return GetDialect(connection).GetTypeMap<T>().QuotedTableName;
        }

        /// <summary>
        /// Gets the quoted name for a column given the property name
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <param name="propertyname">The name of the property to get the column name for</param>
        /// <typeparam name="T">The type to get the column name from</typeparam>
        /// <returns>The quoted column name</returns>
        public static string QuotedColumnName<T>(this IDbConnection connection, string propertyname)
        {
            return GetDialect(connection).GetTypeMap<T>().QuotedColumnName(propertyname);
        }

        /// <summary>
        /// Gets the type map for a given type
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <typeparam name="T">The type to get the map from</typeparam>
        /// <returns>The table mapping for the type</returns>
        public static TableMapping GetTypeMap<T>(this IDbConnection connection)
        {
            return GetDialect(connection).GetTypeMap<T>();
        }

        /// <summary>
        /// Gets the type map for a given type
        /// </summary>
        /// <param name="connection">The connection to get the dialect from</param>
        /// <param name="type">The type to get the map from</param>
        /// <returns>The table mapping for the type</returns>
        public static TableMapping GetTypeMap(this IDbConnection connection, Type type)
        {
            return GetDialect(connection).GetTypeMap(type);
        }

    }
}
