using System;

namespace Ceen.Database
{
    /// <summary>
    /// Extension methods for allowing dialect class with generic parameters, 
    /// which avoids using the <see name="typeof"> operator
    /// </summary>
    public static class DialectSupport
    {
        /// <summary>
        /// Gets the name for a class
        /// </summary>
        /// <returns>The name.</returns>
        /// <typeparam name="T">The type to get the name from</typeparam>
        public static string GetName<T>(this IDatabaseDialect self) => self.GetName(typeof(T));

        /// <summary>
        /// Creates the type map with a custom table name.
        /// </summary>
        /// <param name="name">The custom table name to use.</param>
        /// <typeparam name="T">The type to build the map for.</typeparam>
        public static void CreateTypeMap<T>(this IDatabaseDialect self, string name) => self.CreateTypeMap(name, typeof(T));

        /// <summary>
        /// Gets the type map for the given type
        /// </summary>
        /// <returns>The type map.</returns>
        /// <typeparam name="T">The type to get the map for.</typeparam>
        public static TableMapping GetTypeMap<T>(this IDatabaseDialect self) => self.GetTypeMap(typeof(T));

        /// <summary>
        /// Returns a create-table sql statement
        /// </summary>
        /// <param name="ifNotExists">Only create table if it does not exist</param>
        /// <typeparam name="T">The datatype to store in the table.</typeparam>
        public static string CreateTableSql<T>(this IDatabaseDialect self, bool ifNotExists = true) => self.CreateTableSql(typeof(T), ifNotExists);

        /// <summary>
        /// Creates the select command for the given type.
        /// </summary>
        /// <returns>The select command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        /// <typeparam name="T">The type to build the map for.</typeparam>
        public static string CreateSelectCommand<T>(this IDatabaseDialect self) => self.CreateSelectCommand(typeof(T));

        /// <summary>
        /// Creates a command for inserting a record
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <typeparam name="T">The type to generate the command for.</typeparam>
        public static string CreateInsertCommand<T>(this IDatabaseDialect self) => self.CreateInsertCommand(typeof(T), false);

        /// <summary>
        /// Creates a command for inserting a record
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <typeparam name="T">The type to generate the command for.</typeparam>
        public static string CreateInsertOrIgnoreCommand<T>(this IDatabaseDialect self) => self.CreateInsertCommand(typeof(T), true);

        /// <summary>
        /// Creates a command that checks if a table exists
        /// </summary>
        /// <returns>The table exists command.</returns>
        /// <typeparam name="T">The type to generate the command for.</typeparam>
        public static string CreateTableExistsCommand<T>(this IDatabaseDialect self) => self.CreateTableExistsCommand(typeof(T));
    }
}
