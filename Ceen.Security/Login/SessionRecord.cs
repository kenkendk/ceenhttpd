using System;
namespace Ceen.Security.Login
{
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
		public string Cookie { get; set; }
		/// <summary>
		/// Gets or sets the XSRF Token.
		/// </summary>
		public string XSRFToken { get; set; }
		/// <summary>
		/// Gets or sets the expiration time.
		/// </summary>
		public DateTime Expires { get; set; }
	}
}
