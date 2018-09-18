using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Ceen.Httpd.Handler
{
    /// <summary>
    /// Basic implementation of a file-serving module
    /// </summary>
    public class FileHandler : IHttpModuleWithSetup
    {
        /// <summary>
        /// The folder where files are served from
        /// </summary>
        public string SourceFolder { get; set; }
        /// <summary>
        /// Cached copy of the directory separator as a string
        /// </summary>
        protected static readonly string DIRSEP = Path.DirectorySeparatorChar.ToString();
        /// <summary>
        /// Parser to match Range requests
        /// </summary>
        protected static readonly Regex RANGE_MATCHER = new Regex("bytes=(?<start>\\d*)-(?<end>\\d*)");
        /// <summary>
        /// Chars that are not allowed in the path
        /// </summary>
        protected static readonly string[] FORBIDDENCHARS = new string[] { "\\", "..", ":" };
        /// <summary>
        /// Function that maps a request to a mime type
        /// </summary>
        protected Func<IHttpRequest, string, string> m_mimetypelookup;
        /// <summary>
        /// List of allowed index documents
        /// </summary>
        public string[] IndexDocuments { get; set; } = new string[] { "index.html", "index.htm" };
        /// <summary>
        /// List of allowed index file extensions
        /// </summary>
        public string[] AutoProbeExtensions { get; set; } = new string[] { ".html", ".htm" };
        /// <summary>
        /// The current etag cache
        /// </summary>
        protected readonly Dictionary<string, KeyValuePair<string, long>> m_etagCache = new Dictionary<string, KeyValuePair<string, long>>();
        /// <summary>
        /// The lock used to guard the ETag cache
        /// </summary>
        protected readonly AsyncLock m_etagLock = new AsyncLock();
        /// <summary>
        /// The regular expression used to extract etags from the request
        /// </summary>
        protected readonly Regex ETAG_RE = new Regex(@"\s*(?<isWeak>W\\)?""(?<etag>\w+)""\s*,?");
        /// <summary>
        /// The etag salt in byte-array format
        /// </summary>
        protected byte[] m_etagsalt = null;

        /// <summary>
        /// Gets or sets the etag hashing algorithm.
        /// </summary>
        public string EtagAlgorithm { get; set; } = "MD5";
        /// <summary>
        /// An optional etag salt
        /// </summary>
        public string EtagSalt { get; set; } = null;
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
        /// Enable the ETag header output, which returns an MD5 value for each.
        /// Setting this to less than zero will disable ETag output.
        /// Setting this to zero will emit ETag output, but not cache the results,
        /// causing an etag to be computed for each request.
        /// </summary>
        public int ETagCacheSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the number of seconds the browser is allowed to cache the response.
        /// </summary>
        public int CacheSeconds { get; set; } = 60 * 60 * 24;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceen.Httpd.Handler.FileHandler"/> class.
        /// </summary>
        /// <param name="sourcefolder">The folder to server files from.</param>
        public FileHandler(string sourcefolder)
            : this(sourcefolder, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ceen.Httpd.Handler.FileHandler"/> class.
        /// </summary>
        /// <param name="sourcefolder">The folder to server files from.</param>
        /// <param name="mimetypelookup">A mapping function to return the mime type for a given path.</param>
        public FileHandler(string sourcefolder, Func<IHttpRequest, string, string> mimetypelookup = null)
        {
            SourceFolder = sourcefolder;
            m_mimetypelookup = mimetypelookup ?? DefaultMimeTypes;
        }

        /// <summary>
        /// An overrideable method to hook in logic before
        /// flushing the headers and sending content, allows
        /// an overriding class to alter the response
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="sourcedata">The file with the source data</param>
        public virtual Task BeforeResponseAsync(IHttpContext context, Stream sourcedata)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Computes the ETag value for a given resources
        /// </summary>
        /// <returns>The ETag value.</returns>
        /// <param name="sourcedata">The source stream to compute the ETag for.</param>
        public virtual async Task<string> ComputeETag(Stream sourcedata)
        {
            var buffer = new byte[8 * 1024];
            using (var hasher = string.IsNullOrWhiteSpace(EtagAlgorithm) ? System.Security.Cryptography.MD5.Create() : System.Security.Cryptography.HashAlgorithm.Create(EtagAlgorithm))
            {
                if (m_etagsalt != null)
                    hasher.TransformBlock(m_etagsalt, 0, m_etagsalt.Length, m_etagsalt, 0);

                int r = 0;
                while ((r = await sourcedata.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    hasher.TransformBlock(buffer, 0, r, buffer, 0);
                hasher.TransformFinalBlock(buffer, 0, 0);
                return Convert.ToBase64String(hasher.Hash).TrimEnd('=');
            }
        }

        /// <summary>
        /// Helper method to report the given range as invalid
        /// </summary>
        /// <returns><c>true</c></returns>
        /// <param name="context">The request context.</param>
        /// <param name="bytecount">The byte count to report</param>
        private bool SetInvalidRangeHeader(IHttpContext context, long bytecount)
        {
            context.Response.StatusCode = HttpStatusCode.RangeNotSatisfiable;
            context.Response.Headers["Content-Range"] = "bytes */" + bytecount;
            return true;
        }

        /// <summary>
        /// Helper method to report 304 - Not modified to the client
        /// </summary>
        /// <returns><c>true</c></returns>
        /// <param name="context">The request context.</param>
        /// <param name="etag">The resource ETag, if any.</param>
        private bool SetNotModified(IHttpContext context, string etag)
        {
            context.Response.StatusCode = HttpStatusCode.NotModified;
            context.Response.ContentLength = 0;
            context.Response.SetExpires(TimeSpan.FromSeconds(CacheSeconds));
            if (!string.IsNullOrWhiteSpace(etag))
                context.Response.Headers["ETag"] = $"\"{etag}\"";
            return true;
        }

        /// <summary>
        /// Extracts and validates the local path from the remote request
        /// </summary>
        /// <returns>The local path.</returns>
        /// <param name="context">The request context.</param>
        protected virtual string GetLocalPath(IHttpContext context)
        {
            var pathrequest = Uri.UnescapeDataString(context.Request.Path);

            foreach (var c in FORBIDDENCHARS)
                if (pathrequest.Contains(c))
                    throw new HttpException(HttpStatusCode.BadRequest);

            if (!pathrequest.StartsWith(PathPrefix, StringComparison.Ordinal))
                return null;

            pathrequest = pathrequest.Substring(PathPrefix.Length);


            var path = pathrequest.Replace("/", DIRSEP);
            while (path.StartsWith(DIRSEP, StringComparison.Ordinal))
                path = path.Substring(1);

            path = Path.Combine(SourceFolder, path);
            if (!path.StartsWith(SourceFolder, StringComparison.Ordinal))
                throw new HttpException(HttpStatusCode.BadRequest);

            return path;
        }

        /// <summary>
        /// Performs internal redirects on paths with missing trailings slashes
        /// and handles redirects to index files
        /// </summary>
        /// <returns><c>true</c>, if redirect was issued, <c>false</c> otherwise.</returns>
        /// <param name="path">The local path to use.</param>
        /// <param name="context">The request context.</param>
        protected virtual bool AutoRedirect(string path, IHttpContext context)
        {
            if (!File.Exists(path))
            {
                if (string.IsNullOrWhiteSpace(Path.GetExtension(path)) && !path.EndsWith("/", StringComparison.Ordinal) && !path.EndsWith(".", StringComparison.Ordinal))
                {
                    var ix = AutoProbeExtensions.FirstOrDefault(p => File.Exists(path + p));
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
                        if (!IndexDocuments.Any(p => File.Exists(Path.Combine(path, p))))
                            throw new HttpException(HttpStatusCode.NotFound);

                        context.Response.Redirect(context.Request.Path + "/");
                        return true;
                    }

                    var ix = IndexDocuments.FirstOrDefault(p => File.Exists(Path.Combine(path, p)));
                    if (!string.IsNullOrWhiteSpace(ix))
                    {
                        context.Response.InternalRedirect(context.Request.Path + ix);
                        return true;
                    }
                }

            }

            return false;
        }

        /// <summary>
        /// Serves the request
        /// </summary>
        /// <returns>An awaitable task.</returns>
        /// <param name="path">The local path to a file to send.</param>
        /// <param name="mimetype">The mime type to report.</param>
        /// <param name="context">The request context.</param>
        protected virtual async Task<bool> ServeRequest(string path, string mimetype, IHttpContext context)
        {
            try
            {
                string etag = null;
                string etagkey = ETagCacheSize < 0 ? null : File.GetLastWriteTimeUtc(path).Ticks + path;
                string[] clientetags = new string[0];

                if (etagkey != null)
                {
                    KeyValuePair<string, long> etagcacheddata;
                    using (await m_etagLock.LockAsync())
                        m_etagCache.TryGetValue(etagkey, out etagcacheddata);

                    etag = etagcacheddata.Key;

                    var ce = ETAG_RE.Matches(context.Request.Headers["If-None-Match"] ?? string.Empty);
                    if (ce.Count > 0)
                    {
                        clientetags = new string[ce.Count];
                        for (var i = 0; i < clientetags.Length; i++)
                        {
                            clientetags[i] = ce[i].Groups["etag"].Value;
                            if (etag != null && string.Equals(clientetags[i], etag, StringComparison.OrdinalIgnoreCase))
                                return SetNotModified(context, etag);
                        }
                    }
                }

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
                            return SetInvalidRangeHeader(context, bytecount);

                        if (m.Groups["start"].Length != 0)
                            if (!long.TryParse(m.Groups["start"].Value, out startoffset))
                                return SetInvalidRangeHeader(context, bytecount);

                        if (m.Groups["end"].Length != 0)
                            if (!long.TryParse(m.Groups["end"].Value, out endoffset))
                                return SetInvalidRangeHeader(context, bytecount);

                        if (m.Groups["start"].Length == 0 && m.Groups["end"].Length == 0)
                            return SetInvalidRangeHeader(context, bytecount);

                        if (m.Groups["start"].Length == 0 && m.Groups["end"].Length != 0)
                        {
                            startoffset = bytecount - endoffset;
                            endoffset = bytecount - 1;
                        }

                        if (endoffset > bytecount - 1)
                            endoffset = bytecount - 1;

                        if (endoffset < startoffset)
                            return SetInvalidRangeHeader(context, bytecount);
                    }

                    if (etagkey != null)
                    {
                        fs.Position = 0;
                        etag = await ComputeETag(fs);

                        if (ETagCacheSize > 0)
                        {
                            using (await m_etagLock.LockAsync())
                            {
                                m_etagCache[etagkey] = new KeyValuePair<string, long>(etag, DateTime.UtcNow.Ticks);

                                if (m_etagCache.Count > ETagCacheSize)
                                {
                                    // Don't repeatedly remove items,
                                    // but batch up the removal,
                                    // as the sorting takes some time
                                    var removecount = Math.Max(1, m_etagCache.Count / 3);

                                    foreach (var key in m_etagCache.OrderBy(x => x.Value.Value).Select(x => x.Key).Take(removecount).ToArray())
                                        m_etagCache.Remove(key);
                                }
                            }
                        }
                    }

                    if (etag != null && clientetags != null && clientetags.Any(x => string.Equals(x, etag, StringComparison.Ordinal)))
                        return SetNotModified(context, etag);

                    var lastmodified = File.GetLastWriteTimeUtc(path);
                    context.Response.ContentType = mimetype;
                    context.Response.StatusCode = HttpStatusCode.OK;
                    context.Response.AddHeader("Last-Modified", lastmodified.ToString("R", CultureInfo.InvariantCulture));
                    context.Response.AddHeader("Accept-Ranges", "bytes");
                    context.Response.SetExpires(TimeSpan.FromSeconds(CacheSeconds));

                    DateTime modifiedsincedate;
                    DateTime.TryParseExact(context.Request.Headers["If-Modified-Since"], CultureInfo.CurrentCulture.DateTimeFormat.RFC1123Pattern, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out modifiedsincedate);

                    if (modifiedsincedate == lastmodified)
                    {
                        return SetNotModified(context, etag);
                    }
                    else
                    {
                        if (etag != null)
                            context.Response.Headers["ETag"] = $"\"{etag}\"";

                        context.Response.ContentLength = endoffset - startoffset + 1;
                        if (context.Response.ContentLength != bytecount)
                        {
                            context.Response.StatusCode = HttpStatusCode.PartialContent;
                            context.Response.AddHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", startoffset, endoffset, bytecount));
                        }
                    }

                    await BeforeResponseAsync(context, fs);

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

        #region IHttpModule implementation
        /// <summary>
        /// Handles the request.
        /// </summary>
        /// <returns>The awaitable task.</returns>
        /// <param name="context">The http context.</param>
        public virtual Task<bool> HandleAsync(IHttpContext context)
        {
            if (!string.Equals(context.Request.Method, "GET", StringComparison.Ordinal) && !string.Equals(context.Request.Method, "HEAD", StringComparison.Ordinal))
                throw new HttpException(HttpStatusCode.MethodNotAllowed);

            var path = GetLocalPath(context);
            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult(false);

            if (AutoRedirect(path, context))
                return Task.FromResult(true);

            if (!File.Exists(path))
                throw new HttpException(HttpStatusCode.NotFound);

            // If this is just a rewrite handler, stop now as we did not handle it
            if (RedirectOnly)
                return Task.FromResult(false);

            var mimetype = m_mimetypelookup(context.Request, path);
            if (mimetype == null)
                throw new HttpException(HttpStatusCode.NotFound);

            return ServeRequest(path, mimetype, context);
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
                case ".gz":
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

        /// <summary>
        /// Handles post-configuration setup
        /// </summary>
        public virtual void AfterConfigure()
        {
            if (!string.IsNullOrWhiteSpace(EtagSalt))
                m_etagsalt = System.Text.Encoding.UTF8.GetBytes(EtagSalt);

            SourceFolder = Path.GetFullPath(SourceFolder);

            IndexDocuments = IndexDocuments ?? new string[0];
            AutoProbeExtensions =
                (AutoProbeExtensions ?? new string[0])
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.StartsWith(".", StringComparison.Ordinal) ? x : "." + x)
                    .ToArray();
        }
    }
}

