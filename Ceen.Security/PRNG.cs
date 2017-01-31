using System;
using System.Security.Cryptography;

namespace Ceen.Security
{
	/// <summary>
	/// Class for providing cryptographic strength random data
	/// </summary>
	public static class PRNG
	{
		/// <summary>
		/// The lock used to guard the generation, because the generator is not guaranteed to be thread-safe
		/// </summary>
		private static readonly object _lock = new object();
		/// <summary>
		/// The PRNG instance to use
		/// </summary>
		private static readonly RandomNumberGenerator _prng = RandomNumberGenerator.Create();

		/// <summary>
		/// Fills the target array with random data
		/// </summary>
		/// <param name="target">The array to fill with random data into.</param>
		/// <returns>The target array reference, passed to the function</returns>
		public static byte[] GetRandomBytes(byte[] target)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));
			
			lock(_lock)
				_prng.GetBytes(target);

			return target;
		}

		/// <summary>
		/// Gets a set of random bytes.
		/// </summary>
		/// <param name="target">The array to write into.</param>
		/// <param name="offset">The offset into the array to start writing.</param>
		/// <param name="length">The number of bytes to write.</param>
		public static void GetRandomBytes(byte[] target, int offset, int length)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));
			if (offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (length < 0)
				throw new ArgumentOutOfRangeException(nameof(length));
			if (offset > target.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (offset + length > target.Length)
				throw new ArgumentOutOfRangeException(nameof(length));

			Array.Copy(GetRandomBytes(new byte[length]), 0, target, offset, length);
		}

		/// <summary>
		/// Gets a random base-64 encoded string
		/// </summary>
		/// <returns>The random string.</returns>
		/// <param name="length">The number of bytes to encode.</param>
		public static string GetRandomString(int length)
		{
			return Convert.ToBase64String(GetRandomBytes(new byte[length]));
		}
	}
}
