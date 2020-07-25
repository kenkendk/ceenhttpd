using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// A request to change the email address
    /// </summary>
    public class ChangeEmailRequest
    {
        /// <summary>
        /// The key of the reset request
        /// </summary>
        [PrimaryKey]
        public int ID;

        /// <summary>
        /// The user who requested the email change
        /// </summary>
        public string UserID;

        /// <summary>
        /// The new email address to use
        /// </summary>
        public string NewEmail;

        /// <summary>
        /// The token required to change the email
        /// </summary>
        public string Token;

        /// <summary>
        /// The time the request was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;
    }
}
