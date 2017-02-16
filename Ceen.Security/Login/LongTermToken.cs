using System;
namespace Ceen.Security.Login
{
	/// <summary>
	/// Helper class to
	/// </summary>
	public static partial class Utility
	{
		/// <summary>
		/// Gets a value indicating if the session is valid
		/// </summary>
		/// <returns><c>true</c>, if the session is valid, <c>false</c> otherwise.</returns>
		/// <param name="token">The token to validate.</param>
		public static bool IsNullOrExpired(this LongTermToken token)
		{
			return token == null || token.Expires < DateTime.Now;
		}
	}

	/// <summary>
	/// Class that contains a cookie for long term login support (the remember me function)
	/// </summary>
	public class LongTermToken
	{
		/// <summary>
		/// Gets or sets the user ID this token belongs to.
		/// </summary>
		public string UserID { get; set; }
		/// <summary>
		/// Gets or sets the series value.
		/// </summary>
		public string Series { get; set; }
		/// <summary>
		/// Gets or sets the token value in PKBDF2 format.
		/// </summary>
		public string Token { get; set; }
		/// <summary>
		/// Gets or sets the long-term cookie expiration time.
		/// </summary>
		public DateTime Expires { get; set; }
	}
}
