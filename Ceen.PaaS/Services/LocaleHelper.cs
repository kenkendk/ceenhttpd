using Ceen;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Injection point for choosing locales
    /// </summary>
    public static class LocaleHelper
    {
        /// <summary>
        /// The list of supported languages
        /// </summary>
        // TODO: Use loader context and make configurable
        public static string[] LanguagePreferenceOrder { get; set;} = new string[] { "en" };

        /// <summary>
        /// Gets the most matching locale
        /// </summary>
        /// <param name="request">The request instance</param>
        /// <returns>The most matching locale</returns>
        public static string GetBestLocale(IHttpRequest request)
        {
            return (request.GetAcceptMajorLanguage(LanguagePreferenceOrder) ?? Ceen.LanguageTag.English).Primary;
        }

    }
}