using System;
using System.Reflection;

namespace Ceen.Database
{
    public interface IDatabaseDialect
    {
        /// <summary>
        /// Gets the SQL type for a given property
        /// </summary>
        /// <returns>The sql column type.</returns>
        /// <param name="property">The property being examined.</param>
        string GetSqlColumnType(PropertyInfo property);

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
        /// <param name="property">The class to get the name from.</param>
        string GetName(PropertyInfo property);

        /// <summary>
        /// Creates the type map with a custom table name.
        /// </summary>
        /// <param name="name">The custom table name to use.</param>
        /// <typeparam name="type">The type to build the map for.</typeparam>
        void CreateTypeMap<T>(string name);

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
        /// Creates a command that checks if a table exists
        /// </summary>
        /// <returns>The table exists command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        string CreateTableExistsCommand(Type type);
    }
}
