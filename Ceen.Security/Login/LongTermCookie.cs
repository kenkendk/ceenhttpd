using System;
using System.IO;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Encapsulation for a long-term token inside a cookie string
	/// </summary>
	public class LongTermCookie
	{
		/// <summary>
		/// The magic header value in the cookie.
		/// </summary>
		private static readonly byte[] MAGIC_HEADER = System.Text.Encoding.ASCII.GetBytes("ceen");
		/// <summary>
		/// The serialization version.
		/// </summary>
		private const byte VERSION = 1;
		/// <summary>
		/// The raw token data
		/// </summary>
		private byte[] m_rawtoken;

		/// <summary>
		/// The size of the token components
		/// </summary>
		private const int TOKEN_BYTES = 32;

		/// <summary>
		/// The offset into the raw token data where the series is stored
		/// </summary>
		private static readonly int SERIES_OFFSET = MAGIC_HEADER.Length + 1;
		/// <summary>
		/// The offset into the raw token data where the token is stored
		/// </summary>
		private static readonly int TOKEN_OFFSET = MAGIC_HEADER.Length + 1 + TOKEN_BYTES;

		/// <summary>
		/// Gets or sets the version of this token
		/// </summary>
		public int Version { get; private set; } = VERSION;

		/// <summary>
		/// Creates a new long term cookie
		/// </summary>
		public LongTermCookie()
		{
			m_rawtoken = PRNG.GetRandomBytes(new byte[MAGIC_HEADER.Length + 1 + (TOKEN_BYTES * 2)]);

			Array.Copy(MAGIC_HEADER, m_rawtoken, MAGIC_HEADER.Length);
			m_rawtoken[MAGIC_HEADER.Length] = VERSION;
		}

		/// <summary>
		/// Gets the series.
		/// </summary>
		public string Series
		{
			get
			{
				return Convert.ToBase64String(m_rawtoken, SERIES_OFFSET, TOKEN_BYTES, Base64FormattingOptions.None);
			}
			set
			{
				var data = Convert.FromBase64String(value);
				if (data.Length != TOKEN_BYTES)
					throw new ArgumentOutOfRangeException($"The value to {nameof(Series)} must contain exactly {TOKEN_BYTES} bytes");
				Array.Copy(data, 0, m_rawtoken, SERIES_OFFSET, TOKEN_BYTES);
			}
		}

		/// <summary>
		/// Gets the token.
		/// </summary>
		public string Token
		{
			get
			{
				return Convert.ToBase64String(m_rawtoken, TOKEN_OFFSET, TOKEN_BYTES, Base64FormattingOptions.None);
			}
			set
			{
				var data = Convert.FromBase64String(value);
				if (data.Length != TOKEN_BYTES)
					throw new ArgumentOutOfRangeException($"The value to {nameof(Token)} must contain exactly {TOKEN_BYTES} bytes");
				Array.Copy(data, 0, m_rawtoken, TOKEN_OFFSET, TOKEN_BYTES);
			}
		}

		/// <summary>
		/// Gets a value indicating whether this <see cref="T:Ceen.Httpd.Handler.Login.PBKDF2Token"/> is valid.
		/// </summary>
		/// <value><c>true</c> if is valid; otherwise, <c>false</c>.</value>
		public bool IsValid { get { return m_rawtoken != null; }}

		/// <summary>
		/// Creates a PBKDF2 token from a string.
		/// This method does not throw exceptions but sets the <see cref="IsValid"/> flag if the data was not accepted
		/// </summary>
		/// <param name="token">The serialized token.</param>
		public LongTermCookie(string token)
		{
			try
			{
				var decoded = Convert.FromBase64String(token ?? string.Empty);
				if (decoded.Length != TOKEN_OFFSET + TOKEN_BYTES)
					return;

				for (var i = 0; i < MAGIC_HEADER.Length; i++)
					if (decoded[i] != MAGIC_HEADER[i])
						return;
				
				if (decoded[MAGIC_HEADER.Length] != VERSION)
					return;

				m_rawtoken = decoded;
			}
			catch
			{
			}
		}

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:Ceen.Httpd.Handler.Login.PBKDF2Token"/>.
		/// </summary>
		/// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:Ceen.Httpd.Handler.Login.PBKDF2Token"/>.</returns>
		public override string ToString()
		{
			return Convert.ToBase64String(m_rawtoken);
		}
	}
}
