using System;
using Ceen.Database;

namespace Ceen.Security.Login
{
    /// <summary>
    /// Helper class for static methods on records
    /// </summary>
    public static partial class Utility
	{
		/// <summary>
		/// Gets a value indicating if the session is valid
		/// </summary>
		/// <returns><c>true</c>, if the session is valid, <c>false</c> otherwise.</returns>
		/// <param name="session">The session to validate.</param>
		public static bool IsNullOrExpired(this SessionRecord session)
		{
			return session == null || session.Expires < DateTime.Now;
		}
	}

	/// <summary>
	/// Implementation of a session record
	/// </summary>
	public class SessionRecord
	{
		/// <summary>
		/// The user ID
		/// </summary>
		public string UserID { get; set; }
		/// <summary>
		/// Gets or sets the cookie.
		/// </summary>
        [Unique]
		public string Cookie { get; set; }
        /// <summary>
        /// Gets or sets the XSRF Token.
        /// </summary>
        [Unique]
        public string XSRFToken { get; set; }
		/// <summary>
		/// Gets or sets the expiration time.
		/// </summary>
		public DateTime Expires { get; set; }
	}
}
