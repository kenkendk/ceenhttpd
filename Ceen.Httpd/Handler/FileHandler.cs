using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ceen.Httpd.Handler
{
	/// <summary>
	/// Basic implementation of a file-serving module
	/// </summary>
	public class FileHandler : IHttpModule
	{
		/// <summary>
		/// The folder where files are served from
		/// </summary>
		protected readonly string m_sourcefolder;
		/// <summary>
		/// Cached copy of the directory separator as a string
		/// </summary>
		private static readonly string DIRSEP = Path.DirectorySeparatorChar.ToString();
		/// <summary>
		/// Parser to match Range requests
		/// </summary>
		private static readonly Regex RANGE_MATCHER = new Regex("bytes=(?<start>\\d*)-(?<end>\\d*)");
		/// <summary>
		/// Chars that are not allowed in the path
		/// </summary>
		private static readonly string[] FORBIDDENCHARS = new string[]{ "\\", "..", ":" };
		/// <summary>
		/// Function that maps a request to a mime type
		/// </summary>
		private Func<IHttpRequest, string, string> m_mimetypelookup;
		/// <summary>
		/// List of allowed index files
		/// </summary>
		private readonly string[] m_indexfiles;
		/// <summary>
		/// List of allowed index file extensions
		/// </summary>
		private readonly string[] m_autoprobeextensions;

		/// <summary>
		/// Gets or sets the path prefix
		/// </summary>
		public string PathPrefix { get; set; } = "";

		/// <summary>
		/// Gets or sets a value indicating if this module is simply acting as a rewrite filter,
		/// that is it converts /test to /test.html or /test/index.html if possible.
		/// Using a rewrite filter can simplify other filters as you can write a *.html filter,
		/// and avoid other triggers activating on non *.html files
		/// </summary>
		public bool RedirectOnly { get; set; } = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Ceen.Httpd.Handler.FileHandler"/> class.
		/// </summary>
		/// <param name="sourcefolder">The folder to server files from.</param>
		public FileHandler(string sourcefolder)
			: this(sourcefolder, new string[] { "index.html", "index.htm" }, new string[] { ".html", ".htm" }, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.Httpd.Handler.FileHandler"/> class.
		/// </summary>
		/// <param name="sourcefolder">The folder to server files from.</param>
		/// <param name="mimetypelookup">A mapping function to return the mime type for a given path.</param>
		public FileHandler(string sourcefolder, Func<IHttpRequest, string, string> mimetypelookup)
			: this(sourcefolder, new string[] {"index.html", "index.htm"}, new string[] { ".html", ".htm" }, mimetypelookup)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.Httpd.Handler.FileHandler"/> class.
		/// </summary>
		/// <param name="sourcefolder">The folder to server files from.</param>
		/// <param name="indexfiles">List of filenames allowed as index files.</param>
		/// <param name="mimetypelookup">A mapping function to return the mime type for a given path.</param>
		public FileHandler(string sourcefolder, string[] indexfiles, Func<IHttpRequest, string, string> mimetypelookup = null)
			: this(sourcefolder, indexfiles, new string[] { ".html", ".htm" }, mimetypelookup)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.Httpd.Handler.FileHandler"/> class.
		/// </summary>
		/// <param name="sourcefolder">The folder to server files from.</param>
		/// <param name="indexfiles">List of filenames allowed as index files.</param>
		/// <param name="autoprobeextensions">List of automatically probed extensions</param>
		/// <param name="mimetypelookup">A mapping function to return the mime type for a given path.</param>
		public FileHandler(string sourcefolder, string[] indexfiles, string[] autoprobeextensions, Func<IHttpRequest, string, string> mimetypelookup = null)
		{
			m_indexfiles = indexfiles ?? new string[0];
			m_autoprobeextensions = 
				(autoprobeextensions ?? new string[0])
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Select(x => x.StartsWith(".", StringComparison.Ordinal) ? x : "." + x)
					.ToArray();
			
			m_sourcefolder = Path.GetFullPath(sourcefolder);
			if (!m_sourcefolder.StartsWith(DIRSEP, StringComparison.Ordinal))
				m_sourcefolder = DIRSEP + m_sourcefolder;

			m_mimetypelookup = mimetypelookup ?? DefaultMimeTypes;
		}

		/// <summary>
		/// An overrideable method to hook in logic before
		/// flushing the headers and sending content, allows
		/// an overriding class to alter the response
		/// </summary>
		/// <param name="context">Context.</param>
		public virtual Task BeforeResponseAsync(IHttpContext context)
		{
			return Task.FromResult(true);
		}

		#region IHttpModule implementation
		/// <summary>
		/// Handles the request.
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The http context.</param>
		public virtual async Task<bool> HandleAsync(IHttpContext context)
		{
			if (!string.Equals(context.Request.Method, "GET", StringComparison.Ordinal) && !string.Equals(context.Request.Method, "HEAD", StringComparison.Ordinal))
				throw new HttpException(HttpStatusCode.MethodNotAllowed);

			var pathrequest = Uri.UnescapeDataString(context.Request.Path);

			foreach(var c in FORBIDDENCHARS)
				if (pathrequest.Contains(c))
					throw new HttpException(HttpStatusCode.BadRequest);

			if (!pathrequest.StartsWith(PathPrefix, StringComparison.Ordinal))
				return false;

			pathrequest = pathrequest.Substring(PathPrefix.Length);

			var path = MapToLocalPath(pathrequest);
			if (!path.StartsWith(m_sourcefolder, StringComparison.Ordinal))
				throw new HttpException(HttpStatusCode.BadRequest);

			if (!File.Exists(path))
			{
				if (string.IsNullOrWhiteSpace(Path.GetExtension(path)) && !path.EndsWith("/", StringComparison.Ordinal) && !path.EndsWith(".", StringComparison.Ordinal))
				{
					var ix = m_autoprobeextensions.FirstOrDefault(p => File.Exists(path + p));
					if (!string.IsNullOrWhiteSpace(ix))
					{
						context.Response.InternalRedirect(context.Request.Path + ix);
						return true;
					}					
				}

				if (Directory.Exists(path))
				{
					if (!context.Request.Path.EndsWith("/", StringComparison.Ordinal))
					{
						if (!m_indexfiles.Any(p => File.Exists(Path.Combine(path, p))))
							throw new HttpException(HttpStatusCode.NotFound);

						context.Response.Redirect(context.Request.Path + "/");
						return true;
					}

					var ix = m_indexfiles.FirstOrDefault(p => File.Exists(Path.Combine(path, p)));
					if (!string.IsNullOrWhiteSpace(ix))
					{
						context.Response.InternalRedirect(context.Request.Path + ix);
						return true;
					}
				}

				// No alternatives available
				throw new HttpException(HttpStatusCode.NotFound);
			}

			// If this is just a rewrite handler, stop now as we did not handle it
			if (RedirectOnly)
				return false;

			var mimetype = m_mimetypelookup(context.Request, path);
			if (mimetype == null)
				throw new HttpException(HttpStatusCode.NotFound);

			try
			{
				using (var fs = File.OpenRead(path))
				{
					var startoffset = 0L;
					var bytecount = fs.Length;
					var endoffset = bytecount - 1;

					var rangerequest = context.Request.Headers["Range"];
					if (!string.IsNullOrWhiteSpace(rangerequest))
					{
						var m = RANGE_MATCHER.Match(rangerequest);
						if (!m.Success || m.Length != rangerequest.Length)
						{
							context.Response.StatusCode = HttpStatusCode.RangeNotSatisfiable;
							context.Response.Headers["Content-Range"] = "bytes */" + bytecount;
							return true;
						}

						if (m.Groups["start"].Length != 0)
							if (!long.TryParse(m.Groups["start"].Value, out startoffset))
							{
								context.Response.StatusCode = HttpStatusCode.RangeNotSatisfiable;
								context.Response.Headers["Content-Range"] = "bytes */" + bytecount;
								return true;
							}

						if (m.Groups["end"].Length != 0)
							if (!long.TryParse(m.Groups["end"].Value, out endoffset))
							{
								context.Response.StatusCode = HttpStatusCode.RangeNotSatisfiable;
								context.Response.Headers["Content-Range"] = "bytes */" + bytecount;
								return true;
							}

						if (m.Groups["start"].Length == 0 && m.Groups["end"].Length == 0)
						{
							context.Response.StatusCode = HttpStatusCode.RangeNotSatisfiable;
							context.Response.Headers["Content-Range"] = "bytes */" + bytecount;
							return true;
						}

						if (m.Groups["start"].Length == 0 && m.Groups["end"].Length != 0)
						{
							startoffset = bytecount - endoffset;
							endoffset = bytecount - 1;
						}

						if (endoffset > bytecount - 1)
							endoffset = bytecount - 1;

						if (endoffset < startoffset)
						{
							context.Response.StatusCode = HttpStatusCode.RangeNotSatisfiable;
							context.Response.Headers["Content-Range"] = "bytes */" + bytecount;
							return true;
						}
					}

					var lastmodified = File.GetLastWriteTimeUtc(path);
					context.Response.ContentType = mimetype;
					context.Response.StatusCode = HttpStatusCode.OK;
					context.Response.AddHeader("Last-Modified", lastmodified.ToString("R", CultureInfo.InvariantCulture));
					context.Response.AddHeader("Accept-Ranges", "bytes");

					DateTime modifiedsincedate;
					DateTime.TryParseExact(context.Request.Headers["If-Modified-Since"], CultureInfo.CurrentCulture.DateTimeFormat.RFC1123Pattern, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out modifiedsincedate);

					if (modifiedsincedate == lastmodified)
					{
						context.Response.StatusCode = HttpStatusCode.NotModified;
						context.Response.ContentLength = 0;
					}
					else
					{
						context.Response.ContentLength = endoffset - startoffset + 1;
						if (context.Response.ContentLength != bytecount)
						{
							context.Response.StatusCode = HttpStatusCode.PartialContent;
							context.Response.AddHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", startoffset, endoffset, bytecount));
						}
					}

					await BeforeResponseAsync(context);

					if (context.Response.StatusCode == HttpStatusCode.NotModified)
						return true;

					if (string.Equals(context.Request.Method, "HEAD", StringComparison.Ordinal))
					{
						if (context.Response.ContentLength != 0)
						{
							context.Response.KeepAlive = false;
							await context.Response.FlushHeadersAsync();
						}
						return true;
					}

					fs.Position = startoffset;
					var remain = context.Response.ContentLength;
					var buf = new byte[8 * 1024];

					using (var os = context.Response.GetResponseStream())
					{
						while (remain > 0)
						{
							var r = await fs.ReadAsync(buf, 0, (int)Math.Min(buf.Length, remain));
							await os.WriteAsync(buf, 0, r);
							remain -= r;
						}
					}
				}
			}
			catch
			{
				throw new HttpException(HttpStatusCode.Forbidden);
			}

			return true;

		}
		#endregion

		/// <summary>
		/// Returns the default mime type for a request
		/// </summary>
		/// <returns>The mime type.</returns>
		/// <param name="request">The request.</param>
		/// <param name="mappedpath">The mapped filepath.</param>
		public static string DefaultMimeTypes(IHttpRequest request, string mappedpath)
		{
			return DefaultMimeTypes(mappedpath);
		}

		/// <summary>
		/// Returns the default mime type for a path
		/// </summary>
		/// <returns>The mime type.</returns>
		/// <param name="mappedpath">The mapped file path.</param>
		public static string DefaultMimeTypes(string mappedpath)
		{
			var ext = Path.GetExtension(mappedpath).ToLowerInvariant();

			switch (ext)
			{
				case ".txt":
					return "text/plain";
				case ".htm":
				case ".html":
					return "text/html; charset=utf-8";
				case ".jpg":
				case ".jpeg":
					return "image/jpg";
				case ".bmp":
					return "image/bmp";
				case ".gif":
					return "image/gif";
				case ".png":
					return "image/png";
				case ".ico":
					return "image/vnd.microsoft.icon";
				case ".css":
					return "text/css";
				case ".gzip":
					return "application/x-gzip";
				case ".zip":
					return "application/x-zip";
				case ".tar":
					return "application/x-tar";
				case ".pdf":
					return "application/pdf";
				case ".rtf":
					return "application/rtf";
				case ".js":
					return "application/javascript";
				case ".au":
					return "audio/basic";
				case ".snd":
					return "audio/basic";
				case ".es":
					return "audio/echospeech";
				case ".mp3":
					return "audio/mpeg";
				case ".mp2":
					return "audio/mpeg";
				case ".mid":
					return "audio/midi";
				case ".wav":
					return "audio/x-wav";
				case ".avi":
					return "video/avi";
				case ".htc":
					return "text/x-component";
				case ".map":
					return "application/json";
				case ".hbs":
					return "application/x-handlebars-template";
				case ".woff":
				case ".woff2":
					return "application/font-woff";
				case ".ttf":
					return "application/font-ttf";
				case ".eot":
					return "application/vnd.ms-fontobject";
				case ".otf":
					return "application/font-otf";
				case ".svg":
					return "application/svg+xml";
				case ".xml":
					return "application/xml";

				default:
					return null;
			}
		}	

		protected virtual string MapToLocalPath(string path)
		{
			path = path.Replace("/", DIRSEP);
			while (path.StartsWith(DIRSEP, StringComparison.Ordinal))
				path = path.Substring(1);

			return Path.Combine(m_sourcefolder, path);
		}
	}
}

