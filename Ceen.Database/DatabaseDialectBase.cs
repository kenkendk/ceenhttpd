using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ceen.Database
{
    /// <summary>
    /// Helper method for handling the database non-specifics of type mapping
    /// </summary>
    public abstract class DatabaseDialectBase : IDatabaseDialect 
    {
        /// <summary>
        /// Flag indicating if the database connection supports multithreaded access
        /// </summary>
        /// <value></value>
        public abstract bool IsMultiThreadSafe { get; }

        /// <summary>
        /// The map of property types
        /// </summary>
        protected Dictionary<Type, TableMapping> m_typeMap = new Dictionary<Type, TableMapping>();

        /// <summary>
        /// The lock for the type map
        /// </summary>
        protected readonly object m_typemapLock = new object();

        /// <summary>
        /// Gets the SQL type for a given property
        /// </summary>
        /// <returns>The sql column type.</returns>
        /// <param name="member">The member being examined.</param>
        public abstract Tuple<string, AutoGenerateAction> GetSqlColumnType(MemberInfo member);

        /// <summary>
        /// Hook point for escaping a name
        /// </summary>
        /// <returns>The escaped name.</returns>
        /// <param name="name">Name.</param>
        public virtual string EscapeName(string name) => name.Replace("'", "");

        /// <summary>
        /// Gets the name for a class
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="type">The class to get the name from.</param>
        public virtual string GetName(Type type)
        {
            return
                EscapeName(
                    type.GetCustomAttributes(true).OfType<NameAttribute>().Select(x => x.Name).FirstOrDefault()
                    ??
                    type.Name
                );
        }

        /// <summary>
        /// Quotes a column or table name in a way that is SQL safe
        /// </summary>
        /// <param name="name">The name to quote</param>
        /// <returns>The quoted name</returns>
        public virtual string QuoteName(string name)
        {
            if ((name ?? throw new ArgumentNullException(nameof(name))).Contains("\""))
                throw new ArgumentException("Cannot quote a name with a \" character in it");

            return $"\"{name}\"";
        }

        /// <summary>
        /// Gets the name for a class
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="member">The member to get the name from.</param>
        public virtual string GetName(MemberInfo member)
        {
            return
                EscapeName(
                    member.GetCustomAttributes(true).OfType<NameAttribute>().Select(x => x.Name).FirstOrDefault()
                    ??
                    member.Name
                );
        }

        /// <summary>
        /// Creates the type map with a custom table name.
        /// </summary>
        /// <param name="name">The custom table name to use.</param>
        /// <param name="type">The type to build the map for.</param>
        public virtual void CreateTypeMap(string name, Type type)
        {
            lock (m_typemapLock)
                m_typeMap.Add(type, new TableMapping(this, type, name));
        }

        /// <summary>
        /// Gets the type map for the given type
        /// </summary>
        /// <returns>The type map.</returns>
        /// <param name="type">The type to get the map for.</param>
        public virtual TableMapping GetTypeMap(Type type)
        {
            lock (m_typemapLock)
            {
                if (!m_typeMap.TryGetValue(type, out var res))
                    m_typeMap.Add(type, res = new TableMapping(this, type));
                return res;
            }
        }


        /// <summary>
        /// Returns a create-table sql statement
        /// </summary>
        /// <param name="recordtype">The datatype to store in the table.</param>
        /// <param name="ifNotExists">Only create table if it does not exist</param>
        public abstract string CreateTableSql(Type recordtype, bool ifNotExists = true);

        /// <summary>
        /// Returns a delete-table sql statement
        /// </summary>
        /// <param name="recordtype">The datatype to delete from the table.</param>
        /// <param name="ifExists">Only delete table if it exists</param>
        public abstract string DeleteTableSql(Type recordtype, bool ifExists = true);

        /// <summary>
        /// Creates a command that checks if a table exists
        /// </summary>
        /// <returns>The table exists command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        public abstract string CreateTableExistsCommand(Type type);

        /// <summary>
        /// Creates a command that returns the names of the columns in a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The table column select command</returns>
        public abstract string CreateSelectTableColumnsSql(Type type);

        /// <summary>
        /// Creates a command that adds columns to a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <param name="columns">The columns to add</param>
        /// <returns>The command that adds columns</returns>
        public abstract string CreateAddColumnSql(Type type, IEnumerable<ColumnMapping> columns);

        /// <summary>
        /// Returns a where fragment that limits the query
        /// </summary>
        /// <param name="offset">The optional offset to use</param>
        /// <param name="limit">The maximum number of items to use</param>
        /// <returns>The limit fragment</returns>
        public abstract string Limit(long limit, long? offset = null);

        /// <summary>
        /// Renders an SQL where clause from a query element
        /// </summary>
        /// <param name="type">The type to generate the clause for.</param>
        /// <param name="element">The element to use</param>
        /// <returns>The SQL where clause</returns>
        public abstract KeyValuePair<string, object[]> RenderWhereClause(Type type, QueryElement element);

        /// <summary>
        /// Returns an OrderBy fragment
        /// </summary>
        /// <param name="type">The type to generate the clause for.</param>
        /// <param name="order">The order to render</param>
        /// <returns>The SQL order-by fragment</returns>
        public abstract string OrderBy(Type type, QueryOrder order);

        /// <summary>
        /// Renders a full query clause
        /// </summary>
        /// <param name="query">The query to render</param>
        /// <param name="finalize">Flag indicating if the complete method should be called on the query</param>
        /// <returns>The sql statement</returns>
        public abstract KeyValuePair<string, object[]> RenderStatement(Query query, bool finalize = true);
    }
}
