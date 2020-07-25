using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// A password reset request
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// The key of the reset request
        /// </summary>
        [PrimaryKey]
        public int ID;

        /// <summary>
        /// The user who requested the password reset
        /// </summary>
        public string UserID;

        /// <summary>
        /// The token required to reset the password
        /// </summary>
        public string Token;

        /// <summary>
        /// The time the request was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;

    }
}