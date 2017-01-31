using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Interface for an account handler
	/// </summary>
	public interface ILoginEntryModule
	{
		/// <summary>
		/// Returns the user information, or null, for a user with the given name
		/// </summary>
		/// <returns>The login entries.</returns>
		/// <param name="username">The username to get the login tokens for.</param>
		Task<IEnumerable<LoginEntry>> GetLoginEntriesAsync(string username);

		/// <summary>
		/// Adds a login entry to the storage
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		Task AddLoginEntryAsync(LoginEntry record);
	
		/// <summary>
		/// Deletes a login entry from the storage
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to drop.</param>
		Task DropLoginEntryAsync(LoginEntry record);

		/// <summary>
		/// Drops all login entries for the given userid or username.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="userid">The user ID.</param>
		/// <param name="username">The user name.</param>
		Task DropAllLoginEntriesAsync(string userid, string username);

		/// <summary>
		/// Updates the login entry.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to update.</param>
		Task UpdateLoginTokenAsync(LoginEntry record);


	}
}
