using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ceen.Database
{
    /// <summary>
    /// The Implementation of the database dialect for SQLite
    /// </summary>
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
        public Tuple<string, AutoGenerateAction> GetSqlColumnType(MemberInfo member)
        {
            var action = AutoGenerateAction.None;            
            if (member.GetCustomAttributes(true).OfType<CreatedTimestampAttribute>().Any())
                action = AutoGenerateAction.ClientCreateTimestamp;
            if (member.GetCustomAttributes(true).OfType<ChangedTimestampAttribute>().Any())
            {
                if (action != AutoGenerateAction.None)
                    throw new ArgumentException($"The {nameof(CreatedTimestampAttribute)} and {nameof(ChangedTimestampAttribute)} attributes are mutually exclusive");
                action = AutoGenerateAction.ClientChangeTimestamp;
            }

            var custom = member.GetCustomAttributes(true).OfType<DbTypeAttribute>().Select(x => x.Type).FirstOrDefault();
            if (custom != null)
                return new Tuple<string, AutoGenerateAction>(custom, action);

            Type elType;
            if (member is PropertyInfo pi)
                elType = pi.PropertyType;
            else if (member is FieldInfo fi)
                elType = fi.FieldType;
            else
                throw new Exception($"Unexpected member type: {member?.GetType()}");

            var isPrimaryKey = member.GetCustomAttributes<PrimaryKeyAttribute>(true).Any();
            var otherPrimaries = member.DeclaringType
                .GetProperties()
                .Select(x => x.GetCustomAttributes<PrimaryKeyAttribute>(true).Any())
                .Concat(
                    member.DeclaringType
                        .GetFields()
                        .Select(x => x.GetCustomAttributes<PrimaryKeyAttribute>(true).Any())
                )
                .Any(x => x);

            if (INTTYPES.Contains(elType))
            {
                // If this member is decorated primary, or named ID with no other primary key members,
                // we make this item the primary key
                if (isPrimaryKey || (member.Name == "ID" && !otherPrimaries && action == AutoGenerateAction.None))
                {
                    if (action != AutoGenerateAction.None)
                        throw new ArgumentException($"The primary key cannot also be a timestamp");

                    return new Tuple<string, AutoGenerateAction>("INTEGER PRIMARY KEY AUTOINCREMENT", AutoGenerateAction.DatabaseAutoID);
                }

                return new Tuple<string, AutoGenerateAction>("INTEGER", action);
            }
            else if (elType == typeof(DateTime))
                return new Tuple<string, AutoGenerateAction>("DATETIME", action);
            else if (elType == typeof(bool))
                return new Tuple<string, AutoGenerateAction>("BOOLEAN", action);
            else
            {
                if (isPrimaryKey || ((member.Name == "ID" || member.Name == "GUID") && !otherPrimaries && action == AutoGenerateAction.None))
                {
                    if (action != AutoGenerateAction.None)
                        throw new ArgumentException($"The primary key cannot also be a timestamp");

                    return new Tuple<string, AutoGenerateAction>("STRING PRIMARY KEY", AutoGenerateAction.ClientGenerateGuid);
                }

                return new Tuple<string, AutoGenerateAction>("STRING", action);
            }
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
                            .Select(x => string.Format(@"{0} {1}", QuoteName(x.ColumnName), x.SqlType))
                );

            var constr =
                (mapping.Uniques == null || mapping.Uniques.Length == 0)
                ? string.Empty
                : ", " + string.Join(", ",
                    mapping.Uniques.Select(x =>
                       string.Format($@"CONSTRAINT {QuoteName(mapping.Name + (x.Group == null ? string.Empty : EscapeName(x.Group)) + "_unique")} UNIQUE({string.Join(", ", x.Columns.Select(y => QuoteName(y.ColumnName)))})")
                    )
                );

            var sql = string.Format(
                @"CREATE TABLE{0} {1} ({2} {3}) ",
                ifNotExists ? " IF NOT EXISTS" : "",
                QuoteName(mapping.Name),
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
                $"SELECT {string.Join(",", mapping.AllColumns.Select(x => QuoteName(x.ColumnName)))} FROM {QuoteName(mapping.Name)}";
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
            var statement =
                $"INSERT INTO {QuoteName(mapping.Name)} ({string.Join(",", mapping.InsertColumns.Select(x => QuoteName(x.ColumnName)))}) VALUES ({string.Join(",", mapping.InsertColumns.Select(x => "?"))})";

            if (mapping.IsPrimaryKeyAutogenerated)    
                statement += "; SELECT last_insert_rowid();";

            return statement;
        }

        /// <summary>
        /// Creates a command for deleting one or more items
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The delete command</returns>
        public virtual string CreateDeleteCommand(Type type)
        {
            var mapping = GetTypeMap(type);
            return $"DELETE FROM {QuoteName(mapping.Name)}";
        }

        /// <summary>
        /// Creates a command for deleting an item by suppling the primary key
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The delete command</returns>
        public virtual string CreateDeleteByIdCommand(Type type)
        {
            var mapping = GetTypeMap(type);
            return
                $"DELETE FROM {QuoteName(mapping.Name)} " + $" WHERE " + string.Join(" AND ", mapping.PrimaryKeys.Select(x => $"{QuoteName(x.ColumnName)} = ?"));
        }

        /// <summary>
        /// Creates a command that returns the names of the columns in a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <returns>The table column select command</returns>
        public virtual string CreateSelectTableColumnsSql(Type type)
        {
            var mapping = GetTypeMap(type);
            return 
                $"SELECT \"name\",\"type\" FROM pragma_table_info('{mapping.Name}')";

        }

        /// <summary>
        /// Creates a command that adds columns to a table
        /// </summary>
        /// <param name="type">The type to generate the command for.</param>
        /// <param name="columns">The columns to add</param>
        /// <returns>The command that adds columns</returns>
        public virtual string CreateAddColumnSql(Type type, IEnumerable<ColumnMapping> columns)
        {
            var mapping = GetTypeMap(type);
            if (columns == null || !columns.Any())
                throw new ArgumentException("Cannot create an SQL statement to add zero columns");

            return
                string.Join(";",
                    columns.Select(
                        x =>
                            $"ALTER TABLE {QuoteName(mapping.Name)} ADD COLUMN {QuoteName(x.ColumnName)} {x.SqlType}"
                    )
                );
        }



        /// <summary>
        /// Returns a where fragment that limits the query
        /// </summary>
        /// <param name="offset">The optional offset to use</param>
        /// <param name="limit">The maximum number of items to use</param>
        /// <returns>The limit fragment</returns>
        public virtual string Limit(int limit, int? offset)
        {
            if (offset == null)
                return $"LIMIT {limit}";

            return $"LIMIT {limit} OFFSET {offset.Value}";
        }
    }
}
