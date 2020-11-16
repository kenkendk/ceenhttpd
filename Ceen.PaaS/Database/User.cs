using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// Represents a user
    /// </summary>
    public class User
    {
        /// <summary>
        /// The user ID
        /// </summary>
        [PrimaryKey]        
        public string ID;

        /// <summary>
        /// The sign-up user ID, if this was from a signup
        /// </summary>
        public int SignupID;

        /// <summary>
        /// The users handle or nickname
        /// </summary>
        public string Handle;

        /// <summary>
        /// The real name of the user
        /// </summary>
        public string Name;
        /// <summary>
        /// The name of the user
        /// </summary>
        public string Email;
        /// <summary>
        /// The PBKDF2 password
        /// </summary>
        public string Password;        
        /// <summary>
        /// Flag indicating if the user is disabled
        /// </summary>
        public bool Disabled;
        /// <summary>
        /// The users profile picture
        /// </summary>
        public string AvatarImageID;
        /// <summary>
        /// The users invoice address
        /// </summary>
        public string InvoiceAddress;
        /// <summary>
        /// The users delivery address
        /// </summary>
        public string DeliveryAddress;
        /// <summary>
        /// The users date of birth
        /// </summary>
        public DateTime DateOfBirth;
        /// <summary>
        /// Flag indicating if two-factor authentication is required
        /// </summary>
        public bool Require2FA;


        /// <summary>
        /// The time the user was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;
        /// <summary>
        /// The time the user record was last updated
        /// </summary>
        [ChangedTimestamp]
        public DateTime LastUpdated;

    }
}