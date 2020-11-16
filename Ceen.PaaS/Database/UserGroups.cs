using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// User groups
    /// </summary>
    public class UserGroup
    {
        /// <summary>
        /// The group ID
        /// </summary>
        [PrimaryKey]
        public string ID;
        
        /// <summary>
        /// The group name
        /// </summary>
        public string Name;

        /// <summary>
        /// The time the group was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;
    }

    /// <summary>
    /// A cross table with users
    /// </summary>
    public class UserGroupIndex
    {
        /// <summary>
        /// The group
        /// </summary>
        [Unique("Q")]
        public string GroupID;
        /// <summary>
        /// The user
        /// </summary>
        [Unique("Q")]
        public string UserID;
    }
}