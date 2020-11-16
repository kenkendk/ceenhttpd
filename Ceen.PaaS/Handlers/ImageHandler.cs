using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ceen.PaaS.API
{
    /// <summary>
    /// Handler for the dynamic images,
    /// this is served at /image/{id} and not checked for xsrf or auth tokens
    /// </summary>
    public class ImageHandler : ControllerBase
    {
        /// <summary>
        /// The maximum width or height of an image to serve
        /// </summary>
        private const int MAX_DIMENSIONS = 1024;

        /// <summary>
        /// The path where files are stored
        /// </summary>
        public string StoragePath { get; set; } = System.IO.Path.Combine("content", "images");

        /// <summary>
        /// Resized image cache, LRU key is a synthetic url.
        /// Data is the ETag and stream
        /// </summary>
        private readonly Ceen.LRUCache<KeyValuePair<string, byte[]>> m_resizecache = new Ceen.LRUCache<KeyValuePair<string, byte[]>>(
            sizelimit: 1024*1024*10, 
            countlimit: 10000,
            sizeHandler: (a, b) => Task.FromResult(b.Key.Length * 2 + b.Value.LongLength)
        );

        [HttpGet]
        [Ceen.Mvc.Name("index")]
        [Route("{id}")]
        public async Task<IResult> Get(
            [Parameter(ParameterSource.Url)]
            string id,
            
            [Parameter(ParameterSource.Query, required: false, name: "w")]
            int w = 0,
            [Parameter(ParameterSource.Query, required: false, name: "h")]
            int h = 0
        )
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound;
            
            // Clamp the values
            w = Math.Min(Math.Max(0, w), MAX_DIMENSIONS);
            h = Math.Min(Math.Max(0, h), MAX_DIMENSIONS);

            var mapentry = await DB.RunInTransactionAsync(db => db.SelectItemById<Database.ImageMap>(id));
            if (mapentry == null)
                return NotFound;

            var resp = Context.Response;
            if (w == 0 && h == 0)
            {
                if (!string.IsNullOrWhiteSpace(mapentry.Sha256) && string.Equals(mapentry.Sha256, Context.Request.Headers["If-None-Match"]))
                    return Status(HttpStatusCode.NotModified);

                var fi = new FileInfo(mapentry.Path);
                if (!fi.Exists)
                    return NotFound;

                resp.ContentLength = fi.Length;
                if (!string.IsNullOrWhiteSpace(mapentry.ContentType))
                    resp.ContentType =  mapentry.ContentType;

                resp.StatusCode = HttpStatusCode.OK;
                resp.SetExpires(TimeSpan.FromHours(6));
                if (!string.IsNullOrWhiteSpace(mapentry.Sha256))
                    resp.AddHeader("ETag", mapentry.Sha256);

                await resp.FlushHeadersAsync();
                using(var s = File.OpenRead(mapentry.Path))
                using(var rs = resp.GetResponseStream())
                    await s.CopyToAsync(rs);

                return null;
            }

            var url = SyntheticUrl(id, w, h);

            if (!m_resizecache.TryGetValue(url, out var data))
            {
                using (var img = Image.Load(mapentry.Path, out var format))
                using (var ms = new MemoryStream())
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    var enc = GetEncoder(format.Name);

                    img.Mutate(x => x.Resize(w, h));
                    img.Metadata.ExifProfile = null;
                    img.Save(ms, enc);

                    var bytes = ms.ToArray();
                    data = new KeyValuePair<string, byte[]>(Convert.ToBase64String(sha256.ComputeHash(bytes)), bytes);
                }

                await m_resizecache.AddOrReplaceAsync(url, data);
            }
            else
            {
                if (string.Equals(data.Key, Context.Request.Headers["If-None-Match"]))
                    return Status(HttpStatusCode.NotModified);
            }

            resp.ContentLength = data.Value.Length;
            if (!string.IsNullOrWhiteSpace(mapentry.ContentType))
                resp.ContentType = mapentry.ContentType;

            resp.StatusCode = HttpStatusCode.OK;
            resp.SetExpires(TimeSpan.FromHours(6));
            resp.AddHeader("ETag", data.Key);

            await resp.FlushHeadersAsync();
            using (var rs = resp.GetResponseStream())
                await rs.WriteAsync(data.Value, 0, data.Value.Length);

            return null;
        }

        /// <summary>
        /// Builds a synthetic URL, used as a key in the LRU
        /// </summary>
        /// <param name="id">The image ID</param>
        /// <param name="w">The width of the image</param>
        /// <param name="h">The height of the image</param>
        /// <returns></returns>
        private static string SyntheticUrl(string id, int w, int h)
        {
            return $"w={w}&h={h}&id={id}";
        }

        /// <summary>
        /// Returns the encoder given the format name
        /// </summary>
        /// <param name="format">The name of the format</param>
        /// <returns>The encoder for the format</returns>
        public static SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder(string format)
        {
            switch (format?.ToLowerInvariant())
            {
                case "png":
                    return new SixLabors.ImageSharp.Formats.Png.PngEncoder();
                case "gif":
                    return new SixLabors.ImageSharp.Formats.Gif.GifEncoder();
                case "bmp":
                    return new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();
                case "jpg":
                case "jpeg":
                    return new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder();
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }        
    }

    /// <summary>
    /// Handler for the 
    /// </summary>
    public class ImagesHandler : ControllerBase, IAPIv1
    {
        /// <summary>
        /// Cached instance of the loaded image handler
        /// </summary>
        private ImageHandler m_handler;

        /// <summary>
        /// Returns the storage path from the ImageHandler module
        /// </summary>
        private string StoragePath
        {
            get
            {
                if (m_handler == null)
                    m_handler = Context.GetItemsOfType<ImageHandler>(Context.Current).First();

                return m_handler.StoragePath;
            }
        }

        [Ceen.Mvc.Name("index")]
        [Route("{collection}")]
        [HttpPost]
        public async Task<IResult> Post([Parameter(ParameterSource.Url)]string collection)
        {
            if (string.IsNullOrWhiteSpace(Context.Request.UserID))
                return Forbidden;

            if (string.IsNullOrWhiteSpace(collection))
                return BadRequest;

            if (Context.Request.Files.Count != 1)
                return BadRequest;

            using (var img = Image.Load(Context.Request.Files.First().Data, out var format))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                img.Metadata.ExifProfile = null;
                Directory.CreateDirectory(StoragePath);
                
                var path = Path.Combine(
                    StoragePath, 
                    Path.ChangeExtension(
                        Guid.NewGuid().ToString(), 
                        format.FileExtensions.FirstOrDefault()
                    )
                );

                var enc = ImageHandler.GetEncoder(format.Name);
                img.Save(path, enc);
                string etag;
                using (var fs = File.OpenRead(path))
                    etag = Convert.ToBase64String(sha256.ComputeHash(fs));

                var mapentry = new Database.ImageMap()
                {
                    UserID = Context.Request.UserID,
                    CollectionID = collection,
                    Path = path,
                    Width = img.Width,
                    Height = img.Height,
                    ContentType = format.DefaultMimeType,
                    Sha256 = etag
                };

                await DB.RunInTransactionAsync(db => db.InsertItem(mapentry));
                
                if (collection == "front-page")
                    await Task.Run(() => Cache.InvalidateMainIndexHtmlAsync());

                return Json(new { ID = mapentry.ID });
            }
        }

        /// <summary>
        /// Helper to get the size of a file or -1
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>The file size or -1 if the file does not exist</returns>
        private static long FileSize(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
            }

            return -1;
        }

        public class ResultEntry
        {
            [JsonProperty("id")]
            public string ID;
            [JsonProperty("size")]
            public long Size;
            [JsonProperty("created")]
            public DateTime Created;

            public ResultEntry() {}
            public ResultEntry(Database.ImageMap map)
            {
                this.ID = map.ID;
                this.Size = FileSize(map.Path);
                this.Created = map.Created;
            }
        }

        [HttpGet]
        [Ceen.Mvc.Name("index")]
        [Route("{collection}")]
        public async Task<IResult> Get([Parameter(ParameterSource.Url)]string collection)
        {
            var userid = Context.Request.UserID;
            if (string.IsNullOrWhiteSpace(userid))
                return Forbidden;

            var isAdmin = await Services.AdminHelper.IsAdminAsync(userid);
            if (isAdmin)
            {
                return Json(await DB.RunInTransactionAsync(db =>
                    db.Select<Database.ImageMap>(x => x.CollectionID == collection && (x.UserID == userid || x.UserID == null))
                    .OrderByDescending(x => x.OrderID)
                    .ThenBy(x => x.Created)
                    .Select(x => new ResultEntry(x))
                    .ToArray()
                ));
            }
            else
            {
                return Json(await DB.RunInTransactionAsync(db =>
                    db.Select<Database.ImageMap>(x => x.CollectionID == collection && x.UserID == userid)
                    .Select(x => new ResultEntry(x))
                    .OrderBy(x => x.Created)
                    .ToArray()
                ));
            }
        }

        [HttpPatch]
        [Ceen.Mvc.Name("index")]
        [Route("{collection}")]
        public async Task<IResult> Patch(
            [Parameter(ParameterSource.Url)]
            string collection, 
            
            [Parameter(ParameterSource.Body)]
            ResultEntry[] order)
        {
            if (order == null || order.Length == 0)
                return BadRequest;

            await DB.RunInTransactionAsync(db => {
                // Clear existing orders
                db.Update<Database.ImageMap>(new { OrderID = -1 }, string.Empty);
                // Assign each order
                for(var i = order.Length - 1; i >= 0; i--) {
                    var id = order[i].ID;
                    db.Update<Database.ImageMap>(new { OrderID = order.Length - i }, x => x.ID == id);
                }
            });

            return OK;
        }


        [HttpDelete]
        [Ceen.Mvc.Name("index")]
        [Route("{collection}/{id}")]
        public async Task<IResult> Delete(
            [Parameter(ParameterSource.Url)]string collection,
            [Parameter(ParameterSource.Url)]string id            
            )
        {
            var userid = Context.Request.UserID;
            if (string.IsNullOrWhiteSpace(userid))
                return Forbidden;

            var isAdmin = await Services.AdminHelper.IsAdminAsync(userid);
            return await DB.RunInTransactionAsync(db => {
                var item = db.SelectItemById<Database.ImageMap>(id);
                if (item == null)
                    return NotFound;
                if (item.CollectionID != collection)
                    return NotFound;

                if (item.UserID != userid && !isAdmin)
                    return Forbidden;

                db.DeleteItem(item);
                try { File.Delete(item.Path); }
                catch (Exception ex) {Context.LogErrorAsync($"Failed to delete {item.Path}", ex); }

                if (collection == "front-page")
                    Task.Run(() => Cache.InvalidateMainIndexHtmlAsync());

                return OK;
            });
        }

    }
}