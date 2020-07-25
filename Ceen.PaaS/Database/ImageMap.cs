using System;
using Ceen.Database;

namespace Ceen.PaaS.Database
{
    /// <summary>
    /// The map of all images in the system
    /// </summary>
    public class ImageMap
    {
        /// <summary>
        /// The ID of the image
        /// </summary>
        [PrimaryKey]
        public string ID;

        /// <summary>
        /// The path on disk to the image file
        /// </summary>
        public string Path;

        /// <summary>
        /// The user uploading the image; used to scope visibility for images
        /// </summary>
        public string UserID;

        /// <summary>
        /// The collection the image belongs to, if any
        /// </summary>
        public string CollectionID;

        /// <summary>
        /// The width of the image
        /// </summary>
        public int Width;
        /// <summary>
        /// The height of the image
        /// </summary>
        public int Height;

        /// <summary>
        /// The order of the images, if any
        /// </summary>
        public int OrderID;

        /// <summary>
        /// The content type of the image
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The SHA-256 hash of the file
        /// </summary>
        public string Sha256;

        /// <summary>
        /// The time the image was created
        /// </summary>
        [CreatedTimestamp]
        public DateTime Created;

        /// <summary>
        /// The time the image was last updated
        /// </summary>
        [ChangedTimestamp]
        public DateTime LastUpdated;

    }
}