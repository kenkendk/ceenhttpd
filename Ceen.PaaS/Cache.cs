using System;
using System.Threading.Tasks;
using Ceen;
using static Ceen.Extras.MemCache;

namespace Ceen.PaaS
{
    /// <summary>
    /// Helper for caching items with dependencies
    /// </summary>
    public class Cache
    {
        /// <summary>
        /// The memcache key
        /// </summary>
        private const string KEY = "";

        /// <summary>
        /// The main index.html contents
        /// </summary>
        private readonly CacheHelperInstance<byte[]> m_mainIndexHtml = Helper<byte[]>(KEY, "main-index-html");
        /// <summary>
        /// The front-page dynamic contents in preloaded form
        /// </summary>
        private readonly CacheHelperInstance<string> m_preloadFrontPage = Helper<string>(KEY, "preload-front-page");
        /// <summary>
        /// The rendered terms-of-service document
        /// </summary>
        private readonly CacheHelperInstance<string> m_termsOfService = Helper<string>(KEY, "terms-of-service");
        /// <summary>
        /// The rendered privacy policy document
        /// </summary>
        private readonly CacheHelperInstance<string> m_privacyPolicy = Helper<string>(KEY, "privacy-policy");

        /// <summary>
        /// Creates the cache module
        /// </summary>
        public Cache()
        {
            LoaderContext.RegisterSingletonInstance(this);
        }

        /// <summary>
        /// The main index.html contents
        /// </summary>
        public static CacheHelperInstance<byte[]> MainIndexHtml 
            => LoaderContext.SingletonInstance<Cache>().m_mainIndexHtml;

        /// <summary>
        /// The front-page dynamic contents in preloaded form
        /// </summary>
        public static CacheHelperInstance<string> PreloadFrontPage 
            => LoaderContext.SingletonInstance<Cache>().m_preloadFrontPage;

        /// <summary>
        /// Invalidates the cached anonymous index.html, if any
        /// </summary>
        /// <returns>An awaitable task</returns>
        public static Task InvalidateMainIndexHtmlAsync() =>
            Task.WhenAll(
                MainIndexHtml.InvalidateAsync(),
                PreloadFrontPage.InvalidateAsync()
            );

        /// <summary>
        /// The rendered terms-of-service document
        /// </summary>
        public static CacheHelperInstance<string> TermsOfService 
            => LoaderContext.SingletonInstance<Cache>().m_termsOfService;

        /// <summary>
        /// The rendered privacy policy document
        /// </summary>
        public static CacheHelperInstance<string> PrivacyPolicy 
            => LoaderContext.SingletonInstance<Cache>().m_privacyPolicy;
    }
}