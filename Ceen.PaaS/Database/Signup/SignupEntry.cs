using System;
using Ceen.Database;

namespace Ceen.PaaS.Database.Signup
{
    /// <summary>
    /// The signup status
    /// </summary>
    public enum SignupStatus
    {
        /// <summary>The signup is now created</summary>
        Created,
        /// <summary>The signup is ready to be activated</summary>
        ReadyToActivate,
        /// <summary>The signup is activated</summary>
        Activated,
        /// <summary>The signup is confirmed</summary>
        Confirmed,
        /// <summary>The signup has failed</summary>
        Failed
    }

    /// <summary>
    /// Represents a single signup entry
    /// </summary>
    public class SignupEntry
    {
        /// <summary>
        /// The primary key
        /// </summary>
        [PrimaryKey]
        public int ID;
        /// <summary>
        /// The time the user signed up
        /// </summary>
        [CreatedTimestamp]
        public DateTime When;
        /// <summary>
        /// The email the user signed up with
        /// </summary>
        public string Email;
        /// <summary>
        /// The name the used supplied
        /// </summary>
        public string Name;
        /// <summary>
        /// The actication code required to activate this signup
        /// </summary>
        public string ActivationCode;
        /// <summary>
        /// The time the user activated the request
        /// </summary>
        public DateTime WhenActivated;
        /// <summary>
        /// The status of the signup
        /// </summary>
        public SignupStatus Status;
        /// <summary>
        /// The number of signup attempts
        /// </summary>
        public int Attempts;
        /// <summary>
        /// The last signup attempt
        /// </summary>
        public DateTime LastAttempt;
        /// <summary>
        /// The source IP for the signup
        /// </summary>
        public string SourceIP;
        /// <summary>
        /// The user locale when signing up
        /// </summary>
        public string Locale;
    }
}