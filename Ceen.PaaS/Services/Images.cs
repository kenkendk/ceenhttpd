using System;
using System.Linq;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Helper methods for content images
    /// </summary>
    public static class Images
    {
        /// <summary>
        /// The URL to the image handler
        /// </summary>
        public const string IMAGE_HANDLER_PREFIX = "/image/";

        /// <summary>
        /// The URL for no images
        /// </summary>
        public const string NO_IMAGE_URL = "/image/missing-image.png";

        /// <summary>
        /// Returns a URL for an image
        /// </summary>
        /// <param name="id">The ID to create the link for</param>
        /// <param name="width">The forced width of the image, if any</param>
        /// <param name="height">The forced height of the image, if any</param>
        /// <returns>A link string</returns>
        public static string CreateLinkForId(string id, int width = 0, int height = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                return EncodeSize(NO_IMAGE_URL, width, height);

            return EncodeSize($"{IMAGE_HANDLER_PREFIX}{System.Uri.EscapeUriString(id)}", width, height);
        }

        /// <summary>
        /// Helper method to encode the size parameters into the url
        /// </summary>
        /// <param name="url">The url to size code</param>
        /// <param name="width">The forced width of the image, if any</param>
        /// <param name="height">The forced height of the image, if any</param>
        /// <returns>A link string</returns>
        private static string EncodeSize(string url, int width, int height)
        {
            var q = string.Join("&", new string[] {
                width <= 0 ? string.Empty : $"w={width}",
                height <= 0 ? string.Empty : $"h={height}"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return url + (q.Length == 0 ? q : "?" + q);
        }
    }
}