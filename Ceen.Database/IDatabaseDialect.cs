using System;
using System.Reflection;
using System.Collections.Generic;

namespace Ceen.Database
{
    public interface IDatabaseDialect
    {
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
        /// Gets the name for a class
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
        /// Creates the select command for the given type.
        /// </summary>
        /// <returns>The select command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        string CreateSelectCommand(Type type);

        /// <summary>
        /// Creates a command for inserting a record
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        string CreateInsertCommand(Type type);

        /// <summary>
        /// Creates a command for deleting one or more items
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The delete command</returns>
        string CreateDeleteCommand(Type type);

        /// <summary>
        /// Creates a command for deleting an item by suppling the primary key
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The delete command</returns>
        string CreateDeleteByIdCommand(Type type);

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
    }
}
