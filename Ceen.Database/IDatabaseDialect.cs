using System;
using System.Reflection;
using System.Collections.Generic;

namespace Ceen.Database
{
    /// <summary>
    /// Abstraction of methods used to make vendor agnostic type-safe queries to a database
    /// </summary>
    public interface IDatabaseDialect
    {
        /// <summary>
        /// Flag indicating if the database connection supports multithreaded access
        /// </summary>
        /// <value></value>
        bool IsMultiThreadSafe { get; }

        /// <summary>
        /// Gets the SQL type for a given property
        /// </summary>
        /// <returns>The sql column type.</returns>
        /// <param name="member">The member being examined.</param>
        Tuple<string, AutoGenerateAction> GetSqlColumnType(MemberInfo member);

        /// <summary>
        /// Hook point for escaping a name
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="name">Name.</param>
        string EscapeName(string name);

        /// <summary>
        /// Gets the name for a class
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="type">The class to get the name from.</param>
        string GetName(Type type);

        /// <summary>
        /// Gets the name for a class member
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="member">The item to get the name from.</param>
        string GetName(MemberInfo member);

        /// <summary>
        /// Creates the type map with a custom table name.
        /// </summary>
        /// <param name="name">The custom table name to use.</param>
        /// <param name="type">The type to build the map for.</param>
        void CreateTypeMap(string name, Type type);

        /// <summary>
        /// Gets the type map for the given type
        /// </summary>
        /// <returns>The type map.</returns>
        /// <param name="type">The type to get the map for.</param>
        TableMapping GetTypeMap(Type type);

        /// <summary>
        /// The name to quote
        /// </summary>
        /// <returns>The quoted name.</returns>
        /// <param name="name">The name to quote.</param>
        string QuoteName(string name);

        /// <summary>
        /// Returns a create-table sql statement
        /// </summary>
        /// <param name="recordtype">The datatype to store in the table.</param>
        /// <param name="ifNotExists">Only create table if it does not exist</param>
        string CreateTableSql(Type recordtype, bool ifNotExists = true);

        /// <summary>
        /// Creates a command that checks if a table exists
        /// </summary>
        /// <returns>The table exists command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        string CreateTableExistsCommand(Type type);

        /// <summary>
        /// Creates a command that returns the names of the columns in a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The table column select command</returns>
        string CreateSelectTableColumnsSql(Type type);

        /// <summary>
        /// Creates a command that adds columns to a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <param name="columns">The columns to add</param>
        /// <returns>The command that adds columns</returns>
        string CreateAddColumnSql(Type type, IEnumerable<ColumnMapping> columns);

        /// <summary>
        /// Returns a where fragment that limits the query
        /// </summary>
        /// <param name="offset">The optional offset to use</param>
        /// <param name="limit">The maximum number of items to use</param>
        /// <returns>The limit fragment</returns>
        string Limit(int limit, int? offset = null);

        /// <summary>
        /// Renders an SQL where clause from a query element
        /// </summary>
        /// <param name="type">The type to generate the clause for.</param>
        /// <param name="element">The element to use</param>
        /// <param name="order">The query order</param>
        /// <returns>The SQL where clause</returns>
        KeyValuePair<string, object[]> RenderWhereClause(Type type, QueryElement element);

        /// <summary>
        /// Returns an OrderBy fragment
        /// </summary>
        /// <param name="type">The type to generate the clause for.</param>
        /// <param name="order">The order to render</param>
        /// <returns>The SQL order-by fragment</returns>
        string OrderBy(Type type, QueryOrder order);

        /// <summary>
        /// Renders a full query clause
        /// </summary>
        /// <param name="query">The query to render</param>
        /// <param name="finalize">Flag indicating if the complete method should be called on the query</param>
        /// <returns>The sql statement</returns>
        KeyValuePair<string, object[]> RenderStatement(Query query, bool finalize = true);
    }
}
