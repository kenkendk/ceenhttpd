using System;
using Ceen.Database;

namespace Ceen.PaaS.Database.Signup
{
    /// <summary>
    /// A request token
    /// </summary>
    public class RequestToken
    {
        /// <summary>
        /// The primary key (token)
        /// </summary>
        [PrimaryKey]
        public string ID;
        /// <summary>
        /// The time this token was created
        /// </summary>
        public DateTime WhenCreated;
    }
}