using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ceen.Database
{
    /// <summary>
    /// Represents the mapping for a table
    /// </summary>
    public class TableMapping
    {
        /// <summary>
        /// The name of the table in SQL format
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The type being mapped
        /// </summary>
        public readonly Type Type;

        /// <summary>
        /// The database dialect
        /// </summary>
        public readonly IDatabaseDialect Dialect;

        /// <summary>
        /// All the columns, where the key is the property name
        /// </summary>
        public readonly Dictionary<string, ColumnMapping> AllColumnsByPropertyName;
        /// <summary>
        /// All the columns, where the key is the sql column name
        /// </summary>
        public readonly Dictionary<string, ColumnMapping> AllColumnsBySqlName;
        /// <summary>
        /// All the columns for the table
        /// </summary>
        public readonly ColumnMapping[] AllColumns;
        /// <summary>
        /// All columns that are not the primary key
        /// </summary>
        public readonly ColumnMapping[] ColumnsWithoutPrimaryKey;
        /// <summary>
        /// All columns that are primary keys
        /// </summary>
        public readonly ColumnMapping[] PrimaryKeys;

        /// <summary>
        /// The unique mappings
        /// </summary>
        public readonly UniqueMapping[] Uniques;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Database.TableMapping"/> class.
        /// </summary>
        /// <param name="dialect">The database dialect.</param>
        /// <param name="type">The type to mape.</param>
        /// <param name="nameoverride">An optional name override for the table name</param>
        public TableMapping(IDatabaseDialect dialect, Type type, string nameoverride = null)
        {
            Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            Type = type;
            Name = nameoverride ?? dialect.GetName(type);
            AllColumns = type
                .GetProperties()
                .Where(x => !x.GetCustomAttributes<IgnoreAttribute>(true).Any())
                .Select(x => new ColumnMapping(dialect, x))
                .ToArray();

            ColumnsWithoutPrimaryKey = AllColumns.Where(x => !x.IsPrimaryKey).ToArray();
            PrimaryKeys = AllColumns.Where(x => x.IsPrimaryKey).ToArray();
            AllColumnsBySqlName = AllColumns.ToDictionary(x => x.Name, x => x);
            AllColumnsByPropertyName = AllColumns.ToDictionary(x => x.Property.Name, x => x);

            // Build the unique maps
            var uniques = AllColumns
                .Select(x => new Tuple<ColumnMapping, string[]>(x, x.Property.GetCustomAttributes<UniqueAttribute>().Select(y => y.Group).ToArray()))
                .Where(x => x.Item2 != null && x.Item2.Length > 0);

            var solos = uniques.Where(x => x.Item2.Contains(null));
            var groups = uniques
                .SelectMany(x => 
                            x.Item2
                                .Where(y => y != null)
                                .Select(y => new KeyValuePair<string, ColumnMapping>(y, x.Item1))
                )
                .GroupBy(x => x.Key);

            Uniques =
                solos
                    .Select(x => new UniqueMapping(null, new ColumnMapping[] { x.Item1 }))
                    .Concat(
                        groups.Select(x => new UniqueMapping(x.Key, x.Select(y => y.Value).ToArray()))
                    ).ToArray();

        }
    }

    /// <summary>
    /// The mapping of a column
    /// </summary>
    public class ColumnMapping
    {
        /// <summary>
        /// Value indicating if the column is a primary key
        /// </summary>
        public readonly bool IsPrimaryKey;
        /// <summary>
        /// The name of the column
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// The property being mapped
        /// </summary>
        public readonly PropertyInfo Property;
        /// <summary>
        /// The mapped type of the column
        /// </summary>
        public readonly string SqlType;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Database.ColumnMapping"/> class.
        /// </summary>
        /// <param name="dialect">The database dialect.</param>
        /// <param name="property">The property to map.</param>
        public ColumnMapping(IDatabaseDialect dialect, PropertyInfo property)
        {
            Name = dialect.GetName(property);
            IsPrimaryKey = property.GetCustomAttributes<PrimaryKeyAttribute>(true).Any();
            SqlType = dialect.GetSqlColumnType(property);
            Property = property;
        }
    }

    /// <summary>
    /// Unique column mapping
    /// </summary>
    public class UniqueMapping
    {
        /// <summary>
        /// The optional name of the group
        /// </summary>
        public readonly string Group;

        /// <summary>
        /// The columns in the mapping
        /// </summary>
        public readonly ColumnMapping[] Columns;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Database.UniqueMapping"/> class.
        /// </summary>
        /// <param name="group">The group name, if any.</param>
        /// <param name="columns">The columns in the mapping.</param>
        public UniqueMapping(string group, ColumnMapping[] columns)
        {
            Group = group;
            Columns = columns ?? throw new ArgumentNullException(nameof(columns));

        }
    }
}
