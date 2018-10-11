using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ceen.Database
{
    public class DatabaseDialectSQLite : IDatabaseDialect
    {
        /// <summary>
        /// The integer types
        /// </summary>
        private static readonly Type[] INTTYPES = { typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(long), typeof(ulong) };


        /// <summary>
        /// The map of property types
        /// </summary>
        private Dictionary<Type, TableMapping> m_typeMap = new Dictionary<Type, TableMapping>();

        /// <summary>
        /// The lock for the type map
        /// </summary>
        private readonly object m_typemapLock = new object();

        /// <summary>
        /// Gets the SQL type for a given property
        /// </summary>
        /// <returns>The sql column type.</returns>
        /// <param name="property">The property being examined.</param>
        public string GetSqlColumnType(PropertyInfo property)
        {
            var custom = property.GetCustomAttributes(true).OfType<DbTypeAttribute>().Select(x => x.Type).FirstOrDefault();
            if (custom != null)
                return custom;

            if (INTTYPES.Contains(property.PropertyType))
            {
                if (property.Name == "ID")
                    return "INTEGER PRIMARY KEY AUTOINCREMENT";

                return "INTEGER";
            }
            else if (property.PropertyType == typeof(DateTime))
                return "DATETIME";
            else
                return "STRING";
        }

        /// <summary>
        /// Hook point for escaping a name
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="name">Name.</param>
        public virtual string EscapeName(string name)
        {
            return name.Replace("'", "");
        }

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

        public virtual string QuoteName(string name)
        {
            return $"'{name}'";
        }

        /// <summary>
        /// Gets the name for a class
        /// </summary>
        /// <returns>The name.</returns>
        /// <param name="property">The class to get the name from.</param>
        public virtual string GetName(PropertyInfo property)
        {
            return
                EscapeName(
                    property.GetCustomAttributes(true).OfType<NameAttribute>().Select(x => x.Name).FirstOrDefault()
                    ??
                    property.Name
                );
        }

        /// <summary>
        /// Creates the type map with a custom table name.
        /// </summary>
        /// <param name="name">The custom table name to use.</param>
        /// <typeparam name="type">The type to build the map for.</typeparam>
        public void CreateTypeMap<T>(string name)
        {
            CreateTypeMap(name, typeof(T));
        }

        /// <summary>
        /// Creates the type map with a custom table name.
        /// </summary>
        /// <param name="name">The custom table name to use.</param>
        /// <param name="type">The type to build the map for.</param>
        public void CreateTypeMap(string name, Type type)
        {
            lock (m_typemapLock)
                m_typeMap.Add(type, new TableMapping(this, type, name));
        }

        /// <summary>
        /// Gets the type map for the given type
        /// </summary>
        /// <returns>The type map.</returns>
        /// <param name="type">The type to get the map for.</param>
        public TableMapping GetTypeMap(Type type)
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
        public virtual string CreateTableSql(Type recordtype, bool ifNotExists = true)
        {
            var mapping = GetTypeMap(recordtype);

            var fields =
                string.Join(", ", mapping
                            .AllColumns
                            .Select(x => string.Format(@"""{0}"" {1}", x.Name, x.SqlType))
                );

            var constr =
                (mapping.Uniques == null || mapping.Uniques.Length == 0)
                ? string.Empty
                : string.Join(", ",
                    mapping.Uniques.Select(x =>
                       string.Format($@"CONSTRAINT {QuoteName(mapping.Name + (x.Group == null ? string.Empty : EscapeName(x.Group)) + "_unique")} UNIQUE({string.Join(", ", x.Columns.Select(y => QuoteName(y.Name)))})")
                    )
                );

            var sql = string.Format(
                @"CREATE TABLE{0} ""{1}"" ({2} {3}) ",
                ifNotExists ? " IF NOT EXISTS" : "",
                mapping.Name,
                fields,
                constr
            );

            return sql;
        }

        /// <summary>
        /// Creates the select command for the given type.
        /// </summary>
        /// <returns>The select command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        public virtual string CreateSelectCommand(Type type)
        {
            var mapping = GetTypeMap(type);
            return
                $"SELECT {string.Join(",", mapping.AllColumns.Select(x => QuoteName(x.Name)))} FROM {QuoteName(mapping.Name)}";
        }

        /// <summary>
        /// Creates a command that checks if a table exists
        /// </summary>
        /// <returns>The table exists command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        public virtual string CreateTableExistsCommand(Type type)
        {
            var mapping = GetTypeMap(type);
            return
                $"SELECT {QuoteName("name")} FROM sqlite_master WHERE type = {QuoteName("table")} AND name = {QuoteName(mapping.Name)}";
        }

        /// <summary>
        /// Creates a command for inserting a record
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <param name="type">The type to generate the command for.</param>
        public virtual string CreateInsertCommand(Type type)
        {
            var mapping = GetTypeMap(type);
            return
                $"INSERT INTO {QuoteName(mapping.Name)} ({string.Join(",", mapping.AllColumns.Select(x => QuoteName(x.Name)))}) VALUES ({string.Join(",", mapping.AllColumns.Select(x => "?"))})";
        }
    }
}
