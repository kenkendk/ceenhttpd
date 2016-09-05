using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Globalization;

namespace Ceenhttpd.Handler
{
	/// <summary>
	/// Basic implementation of a file-serving module
	/// </summary>
	public class FileHandler : IHttpModule
	{
		/// <summary>
		/// The folder where files are served from
		/// </summary>
		private string m_sourcefolder;
		/// <summary>
		/// Cached copy of the directory separator as a string
		/// </summary>
		private static readonly string DIRSEP = Path.DirectorySeparatorChar.ToString();
		/// <summary>
		/// Chars that are not allowed in the path
		/// </summary>
		private static readonly string[] FORBIDDENCHARS = new string[]{ "\\", "..", ":" };
		/// <summary>
		/// Function that maps a request to a mime type
		/// </summary>
		private Func<HttpRequest, string, string> m_mimetypelookup;
		/// <summary>
		/// List of allowed index files
		/// </summary>
		private readonly string[] m_indexfiles;

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.FileHandler"/> class.
		/// </summary>
		/// <param name="sourcefolder">The folder to server files from.</param>
		/// <param name="mimetypelookup">A mapping function to return the mime type for a given path.</param>
		public FileHandler(string sourcefolder, Func<HttpRequest, string, string> mimetypelookup = null)
			: this(sourcefolder, new string[] {"index.htm", "index.html"}, mimetypelookup)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceenhttpd.FileHandler"/> class.
		/// </summary>
		/// <param name="sourcefolder">The folder to server files from.</param>
		/// <param name="indexfiles">List of filenames allowed as index files.</param>
		/// <param name="mimetypelookup">A mapping function to return the mime type for a given path.</param>
		public FileHandler(string sourcefolder, string[] indexfiles, Func<HttpRequest, string, string> mimetypelookup = null)
		{
			m_indexfiles = indexfiles ?? new string[0];
			m_sourcefolder = Path.GetFullPath(sourcefolder);
			if (!m_sourcefolder.StartsWith(DIRSEP))
				m_sourcefolder = DIRSEP + m_sourcefolder;

			m_mimetypelookup = mimetypelookup ?? DefaultMimeTypes;
		}

		#region IHttpModule implementation
		/// <summary>
		/// Handles the request.
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="request">The request.</param>
		/// <param name="response">The response.</param>
		public async Task<bool> HandleAsync(HttpRequest request, HttpResponse response)
		{
			foreach(var c in FORBIDDENCHARS)
				if (request.Path.Contains(c))
					throw new HttpException(HttpStatusCode.BadRequest);

			var path = MapToLocalPath(request.Path);
			if (!path.StartsWith(m_sourcefolder, StringComparison.Ordinal))
				throw new HttpException(HttpStatusCode.BadRequest);

			if (Directory.Exists(path))
			{
				if (!request.Path.EndsWith("/", StringComparison.Ordinal))
				{
					if (!m_indexfiles.Any(p => File.Exists(Path.Combine(path, p))))
						throw new HttpException(HttpStatusCode.NotFound);

					response.Redirect(request.Path + "/");
					return true;
				}

				var ix = m_indexfiles.Where(p => File.Exists(Path.Combine(path, p))).FirstOrDefault();
				if (!string.IsNullOrWhiteSpace(ix))
					path = Path.Combine(path, ix);
			}

			if (!File.Exists(path))
				throw new HttpException(HttpStatusCode.NotFound);

			var mimetype = m_mimetypelookup(request, path);
			if (mimetype == null)
				throw new HttpException(HttpStatusCode.NotFound);

			response.ContentType = mimetype;
			response.StatusCode = HttpStatusCode.OK;
			response.AddHeader("Last-Modified", File.GetLastWriteTime(path).ToString("R", CultureInfo.InvariantCulture));

			try
			{
				using (var fs = File.OpenRead(path))
				{
					response.ContentLength = fs.Length;

					using(var os = response.GetResponseStream())
						await fs.CopyToAsync(os);
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
		/// <param name="request">The path.</param>
		public static string DefaultMimeTypes(HttpRequest request, string path)
		{
			return DefaultMimeTypes(path);
		}

		/// <summary>
		/// Returns the default mime type for a path
		/// </summary>
		/// <returns>The mime type.</returns>
		/// <param name="request">The path.</param>
		public static string DefaultMimeTypes(string path)
		{
			var ext = path.Substring(path.LastIndexOf('.') + 1).ToLowerInvariant();

			switch (ext)
			{
				case "txt":
					return "text/plain";
				case "htm":
					return "text/html; charset=utf-8";
				case "html":
					return "text/html; charset=utf-8";
				case "jpg":
					return "image/jpg";
				case "jpeg":
					return "image/jpg";
				case "bmp":
					return "image/bmp";
				case "gif":
					return "image/gif";
				case "png":
					return "image/png";
				case "ico":
					return "image/vnd.microsoft.icon";
				case "css":
					return "text/css";
				case "gzip":
					return "application/x-gzip";
				case "zip":
					return "application/x-zip";
				case "tar":
					return "application/x-tar";
				case "pdf":
					return "application/pdf";
				case "rtf":
					return "application/rtf";
				case "js":
					return "application/javascript";
				case "au":
					return "audio/basic";
				case "snd":
					return "audio/basic";
				case "es":
					return "audio/echospeech";
				case "mp3":
					return "audio/mpeg";
				case "mp2":
					return "audio/mpeg";
				case "mid":
					return "audio/midi";
				case "wav":
					return "audio/x-wav";
				case "avi":
					return "video/avi";
				case "htc":
					return "text/x-component";
				case "map":
					return "application/json";
				case "hbs":
					return "application/x-handlebars-template";
				case "woff":
					return "application/font-woff";
				case "ttf":
					return "application/font-ttf";
				default:
					return null;
			}
		}	

		private string MapToLocalPath(string path)
		{
			path = path.Replace("/", DIRSEP);
			while (path.StartsWith(DIRSEP, StringComparison.Ordinal))
				path = path.Substring(1);

			return Path.Combine(m_sourcefolder, path);
		}
	}
}

