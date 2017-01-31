using System;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Interface for implementing a short-term storage implementation.
	/// Beware that all methods need to be thread-safe.
	/// </summary>
	public interface IShortTermStorageModule
	{
		/// <summary>
		/// Gets a session record from a cookie identifier
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="cookie">The cookie identifier.</param>
		Task<SessionRecord> GetSessionFromCookieAsync(string cookie);

		/// <summary>
		/// Gets a session record from an XSRF token
		/// </summary>
		/// <returns>The session record.</returns>
		/// <param name="xsrf">The XSRF token.</param>
		Task<SessionRecord> GetSessionFromXSRFAsync(string xsrf);

		/// <summary>
		/// Adds a new session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		Task AddSessionAsync(SessionRecord record);
		/// <summary>
		/// Drops a session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		Task DropSessionAsync(SessionRecord record);

		/// <summary>
		/// Updates the expiration time on the given session record
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		Task UpdateSessionExpirationAsync(SessionRecord record);

		/// <summary>
		/// Called periodically to expire old items
		/// </summary>
		/// <returns>An awaitable task.</returns>
		Task ExpireOldItemsAsync();
	}
}
