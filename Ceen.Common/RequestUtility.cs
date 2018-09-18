using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen
{
    public static class RequestUtility
    {
		/// <summary>
		/// Gets an encoding from a charset string
		/// </summary>
		/// <returns>The encoding for the charset.</returns>
		/// <param name="charset">The charset string.</param>
        public static Encoding GetEncodingForCharset(string charset)
		{
			if (string.Equals("utf-8", charset, StringComparison.OrdinalIgnoreCase))
				return Encoding.UTF8;
			else if (string.Equals("ascii", charset, StringComparison.OrdinalIgnoreCase))
				return Encoding.ASCII;
			else
				return Encoding.GetEncoding(charset);
		}

		/// <summary>
		/// Gets an encoding from a charset string
		/// </summary>
		/// <returns>The encoding for the charset.</returns>
		/// <param name="contenttype">The content type string.</param>
		public static Encoding GetEncodingForContentType(string contenttype)
        {
            var enc = GetHeaderComponent(contenttype, "encoding");
            if (string.IsNullOrWhiteSpace(enc))
                enc = GetHeaderComponent(contenttype, "charset");

            // Defaults to ASCII (7-bit), unless we are using "application" types which are 8-bit
            if (string.IsNullOrWhiteSpace(enc))
                return
                    contenttype.StartsWith("application/", StringComparison.OrdinalIgnoreCase)
                        ? Encoding.UTF8
                        : Encoding.ASCII;

            return GetEncodingForCharset(enc);
		}

		/// <summary>
		/// Gets an encoding from a charset string
		/// </summary>
		/// <returns>The encoding for the charset.</returns>
		/// <param name="request">The request instance.</param>
		public static Encoding GetEncodingForCharset(this IHttpRequest request)
		{
            return GetEncodingForContentType(request.ContentType);
		}

        /// <summary>
        /// Returns a value indicating if the request is a multi-part request
        /// </summary>
        /// <returns><c>true</c>, if multi-part was used, <c>false</c> otherwise.</returns>
        /// <param name="request">The request to examine.</param>
        public static bool IsMultipartRequest(this IHttpRequest request)
        {
            return IsMultipartRequest(request.ContentType);
        }

		/// <summary>
		/// Returns a value indicating if the request is a multi-part request
		/// </summary>
		/// <returns><c>true</c>, if multi-part was used, <c>false</c> otherwise.</returns>
		/// <param name="contenttype">The request contenttype to examine.</param>
		public static bool IsMultipartRequest(string contenttype)
		{
            return IsContentType(contenttype, "multipart/form-data");
		}

        public static bool IsContentType(string contenttype, string test)
        {
            if (string.IsNullOrWhiteSpace(contenttype))
                return false;

            var firstdelim = contenttype.IndexOfAny(new char[] { ';', ' ', ',' });
            if (firstdelim < 0)
                firstdelim = contenttype.Length - 1;

            return string.Equals(contenttype.Substring(0, firstdelim + 1), test, StringComparison.OrdinalIgnoreCase);
        }

		/// <summary>
		/// Returns a value indicating if the request is a multi-part request
		/// </summary>
		/// <returns><c>true</c>, if multi-part was used, <c>false</c> otherwise.</returns>
		/// <param name="contenttype">The request contenttype to examine.</param>
		public static bool IsJsonRequest(string contenttype)
		{
            var ct = contenttype ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ct))
                return false;

            // First is correct, rest is for compatibility
            return IsContentType(contenttype, "application/json")
                || IsContentType(contenttype, "application/x-javascript")
                || IsContentType(contenttype, "text/javascript")
                || IsContentType(contenttype, "text/x-javascript")
                || IsContentType(contenttype, "text/x-json");
		}

		/// <summary>
		/// Splits a header line into its key-value components
		/// </summary>
		/// <returns>The components.</returns>
		/// <param name="line">The line to split.</param>
		public static IEnumerable<KeyValuePair<string, string>> SplitHeaderLine(string line)
		{
			return (line ?? "").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x =>
				{
					var c = x.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
					var value = (c.Skip(1).FirstOrDefault() ?? "").Trim();
					if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
						value = value.Substring(1, value.Length - 2);
					return new KeyValuePair<string, string>(c.First().Trim(), value);
				});
		}


		/// <summary>
		/// Gets a named component from a header line
		/// </summary>
		/// <returns>The header component or null.</returns>
		/// <param name="line">The header line.</param>
		/// <param name="key">The component to find.</param>
		public static string GetHeaderComponent(string line, string key)
		{
			return
				SplitHeaderLine(line)
				.Where(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
				.Select(x => x.Value)
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads all bytes from a stream into a string, using UTF8 encoding
		/// </summary>
		/// <returns>The string from the stream.</returns>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="token">The cancellation token.</param>
		public static Task<string> ReadAllAsStringAsync(this Stream stream, CancellationToken token = default(CancellationToken))
		{
			return ReadAllAsStringAsync(stream, System.Text.Encoding.UTF8, token);
		}

		/// <summary>
		/// Reads all bytes from a stream into a string
		/// </summary>
		/// <returns>The string from the stream.</returns>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="encoding">The encoding to use.</param>
		/// <param name="token">The cancellation token.</param>
		public static async Task<string> ReadAllAsStringAsync(this Stream stream, System.Text.Encoding encoding, CancellationToken token = default(CancellationToken))
		{
			if (encoding == null)
				throw new ArgumentNullException(nameof(encoding));

			using (var ms = new System.IO.MemoryStream())
			{
				await stream.CopyToAsync(ms, 1024 * 8, token);
				return encoding.GetString(ms.ToArray());
			}
		}
	}
}
