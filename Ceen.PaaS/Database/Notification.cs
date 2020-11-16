using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// The reason for the notification
    /// </summary>
    public enum NotificationReason
    {
        /// <summary>The user received a private message</summary>
        PrivateMessage,
        /// <summary>A new topic is on the news list</summary>
        NewsEntry,
        /// <summary>A new comment was made on a watched topic</summary>
        CommentUpdate,
        /// <summary>An admin sent a message</summary>
        AdminMessage,
        /// <summary>The system sent a message</summary>
        SystemMessage
    }

    /// <summary>
    /// A notification for a user
    /// </summary>
    public class Notification
    {
        /// <summary>
        /// The notification ID
        /// </summary>
        public int ID;

        /// <summary>
        /// The user the notification is for
        /// </summary>
        public string UserID;

        /// <summary>
        /// A title for the message
        /// </summary>
        public string Title;

        /// <summary>
        /// An excerpt of the message 
        /// </summary>
        public string Excerpt;

        /// <summary>
        /// Link to the item being notified
        /// </summary>
        public string Link;

        /// <summary>
        /// The reason for the notification
        /// </summary>
        public NotificationReason Reason;

        /// <summary>
        /// Flag indicating if the user saw the post
        /// </summary>
        public bool Seen;

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