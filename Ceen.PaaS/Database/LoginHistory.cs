using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// A login history item
    /// </summary>
    public class LoginHistory
    {
        /// <summary>
        /// The unique login entry ID
        /// </summary>
        [PrimaryKey]
        public int ID;

        /// <summary>
        /// The user that logged in, if any
        /// </summary>
        public int UserID;

        /// <summary>
        /// The time the login was attempted
        /// </summary>
        public DateTime When;

        /// <summary>
        /// Flag indicating if the login was a success
        /// </summary>
        public bool Success;

        /// <summary>
        /// Flag indicating if the logged in user is an admin user
        /// </summary>
        public bool IsAdmin;

        /// <summary>
        /// Any message, such as reject messages
        /// </summary>
        public string Message;
    }
}