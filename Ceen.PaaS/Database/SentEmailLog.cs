using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// The different types of email supported
    /// </summary>
    public enum EmailType
    {
        /// <summary>The email is something else</summary>
        Other,
        /// <summary>The email is an activation email</summary>
        ActivationEmail,
        /// <summary>The email is a password reset email</summary>
        PasswordReset,
        /// <summary>The email is a signup offer</summary>
        SignupConfirmation,
        /// <summary>The email is an email change request</summary>
        EmailChange,
        /// <summary>The email if is a notification from a watched topic</summary>
        WatchedTopic,
        /// <summary>The email if is a news notification</summary>
        News
    }

    /// <summary>
    /// Log of sent emails
    /// </summary>
    public class SentEmailLog
    {
        /// <summary>
        /// The ID of this log entry
        /// </summary>
        [PrimaryKey]
        public long ID;

        /// <summary>
        /// The email sender
        /// </summary>
        public string From;
        /// <summary>
        /// The email recipient
        /// </summary>
        public string To;
        /// <summary>
        /// The email subject
        /// </summary>
        public string Subject;
        /// <summary>
        /// The time the email was sent
        /// </summary>
        public DateTime When;
        /// <summary>
        /// The email type
        /// </summary>
        public EmailType Type;
        /// <summary>
        /// A value indicting if the email has been delivered to the mail server
        /// </summary>
        public bool Delivered;
        /// <summary>
        /// The IP of the client requesting the email to be sent (for rate limiting)
        /// </summary>
        public string RequestIP;
    }
}