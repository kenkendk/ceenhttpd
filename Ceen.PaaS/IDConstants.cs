namespace Ceen.PaaS
{
    /// <summary>
    /// Constants used for various items to bootstrap the system
    /// </summary>
    public static class IDConstants
    {
        /// <summary>
        /// The admin group ID
        /// </summary>
        public const string AdminGroupID = "builtin:admin";
        /// <summary>
        /// The group ID for admins with access to the SQL pane
        /// </summary>
        public const string SqlAdminGroupID = "builtin:sqladmin";
        /// <summary>
        /// The group ID for admins with access to the log pages
        /// </summary>
        public const string LogAdminGroupID = "builtin:logadmin";
    }
}