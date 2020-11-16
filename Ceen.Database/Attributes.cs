using System;
namespace Ceen.Database
{
    /// <summary>
    /// Overrides the item name
    /// </summary>
    public class NameAttribute : Attribute
    {
        /// <summary>
        /// The name of the item
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Database.NameAttribute"/> class.
        /// </summary>
        /// <param name="name">The name to use.</param>
        public NameAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }

    /// <summary>
    /// The database type
    /// </summary>
    public class DbTypeAttribute : Attribute
    {
        /// <summary>
        /// The type to use
        /// </summary>
        public readonly string Type;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Database.DbTypeAttribute"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        public DbTypeAttribute(string type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }
    }

    /// <summary>
    /// Marker class to make item unique
    /// </summary>
    public class UniqueAttribute : Attribute
    {
        /// <summary>
        /// The unique group
        /// </summary>
        public readonly string Group;

        /// <summary>
        /// Constructs a unique attribute without a group
        /// </summary>
        public UniqueAttribute()
        {
            Group = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Database.DbTypeAttribute"/> class.
        /// </summary>
        /// <param name="group">The unique group.</param>
        public UniqueAttribute(string group)
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
        }
    }

    /// <summary>
    /// Attribute to mark item as primary key
    /// </summary>
    public class PrimaryKeyAttribute : Attribute
    {
    }

    /// <summary>
    /// A column ignore marker
    /// </summary>
    public class IgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// Marker attribute for setting a property or field as a creation timestamp
    /// </summary>
    public class CreatedTimestampAttribute : Attribute
    {
    }

    /// <summary>
    /// Marker attribute for setting a property or field as a change timestamp
    /// </summary>
    public class ChangedTimestampAttribute : Attribute
    {
    }
}
