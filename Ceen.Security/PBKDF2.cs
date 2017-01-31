using System;
using System.Security.Cryptography;

namespace Ceen.Security
{
	/// <summary>
	/// Class that wraps the methods related to PBKDF2 handling
	/// </summary>
	public static class PBKDF2
	{
		/// <summary>
		/// The magic header embedded in each string
		/// </summary>
		private const string MAGIC = "PBKDF2";
		/// <summary>
		/// The version embeded in each string, allows upgrading later
		/// </summary>
		private const int VERSION = 1;
		/// <summary>
		/// The number of rounds to use
		/// </summary>
		private const int ROUNDS = 10000;
		/// <summary>
		/// The length of the salt to use
		/// </summary>
		private const int SALTLEN = 32;
		/// <summary>
		/// The length of the stored key
		/// </summary>
		private const int KEYLEN = 24;

		/// <summary>
		/// Creates a PBKDF2 string from a password.
		/// </summary>
		/// <returns>The PBKDF2 string.</returns>
		/// <param name="value">The password to used.</param>
		public static string CreatePBKDF2(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentNullException(nameof(value));
				
			var data = System.Text.Encoding.UTF8.GetBytes(value);
			var salt = PRNG.GetRandomBytes(new byte[SALTLEN]);

			var pbkdf2 = new Rfc2898DeriveBytes(data, salt, ROUNDS);
			var key = pbkdf2.GetBytes(KEYLEN);

			return $"{MAGIC}${VERSION}${ROUNDS}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
		}

		/// <summary>
		/// Compares the supplied password with the stored PBKDF2 token.
		/// Use this method as the ONLY way to compare tokens, as it takes
		/// care to do this in a way that guards against timing attacks
		/// </summary>
		/// <returns><c>true</c>, if password was correct, <c>false</c> otherwise.</returns>
		/// <param name="password">The password to check.</param>
		/// <param name="previous">The stored PBKDF2 token.</param>
		public static bool ComparePassword(string password, string previous)
		{
			if (string.IsNullOrWhiteSpace(password))
				throw new ArgumentNullException(nameof(password));
			if (string.IsNullOrWhiteSpace(previous))
				throw new ArgumentNullException(nameof(previous));

			var comp = previous.Split('$');
			int rounds = -1;
			if (comp.Length != 5 || comp[0] != MAGIC || comp[1] != VERSION.ToString() || !int.TryParse(comp[2], out rounds) || rounds <= 0 || comp[3].Length != SALTLEN || comp[4].Length != KEYLEN)
				throw new ArgumentException("The supplied PBKDF2 token is invalid", nameof(previous));

			var data = System.Text.Encoding.UTF8.GetBytes(password);
			var salt = Convert.FromBase64String(comp[3]);

			var pbkdf2 = new Rfc2898DeriveBytes(data, salt, rounds);
			var key = Convert.ToBase64String(pbkdf2.GetBytes(comp[4].Length));

			return CompareStringsConstantTime(key, previous);
		}

		/// <summary>
		/// Compares two strings constant time, thus making it harder to check if parts of the result are correct
		/// </summary>
		/// <returns><c>true</c>, if strings are equal, <c>false</c> otherwise.</returns>
		/// <param name="a">One string.</param>
		/// <param name="b">Another string.</param>
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
		private static bool CompareStringsConstantTime(string a, string b)
		{
			var len = Math.Min(a.Length, b.Length);
			var res = 0;

			for (int i = 0; i < len; i++)
				res |= a[i] ^ b[i];

			return res == 0;
		}
	}
}
