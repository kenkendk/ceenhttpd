using System;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Interface for a class that can store long-term login information.
	/// Beware that all methods need to be thread-safe.
	/// </summary>
	public interface ILongTermStorageModule
	{
		/// <summary>
		/// Gets a long-term login entry
		/// </summary>
		/// <returns>The long term login entry.</returns>
		/// <param name="series">The series identifier to use for lookup.</param>
		Task<LongTermToken> GetLongTermLoginAsync(string series);

		/// <summary>
		/// Adds a long term login entry
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">The record to add.</param>
		Task AddLongTermLoginAsync(LongTermToken record);
		/// <summary>
		/// Drops the given long term login entry.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="record">Record.</param>
		Task DropLongTermLoginAsync(LongTermToken record);
		/// <summary>
		/// Drops all long term logins for a given user.
		/// </summary>
		/// <returns>An awaitable task.</returns>
		/// <param name="userid">The user for whom the long term logins must be dropped.</param>
		/// <param name="series">The series identifier for the login token that caused the issuance.</param>
		Task DropAllLongTermLoginsAsync(string userid, string series);

		/// <summary>
		/// Called periodically to expire old items
		/// </summary>
		/// <returns>An awaitable task.</returns>
		Task ExpireOldItemsAsync();
	}
}
