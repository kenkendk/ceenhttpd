using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// A request to activate a signup
    /// </summary>
    public class ActivationRequest
    {
        /// <summary>
        /// The key of the activation request
        /// </summary>
        [PrimaryKey]
        public int ID;

        /// <summary>
        /// The ID of the signup request being activated
        /// </summary>
        public string SignupID;

        /// <summary>
        /// The token required to complete the activation
        /// </summary>
        public string Token;

        /// <summary>
        /// The time the request was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;

        /// <summary>
        /// The time the entry was last used to send an email
        /// </summary>
        public DateTime LastSent;
    }
}
