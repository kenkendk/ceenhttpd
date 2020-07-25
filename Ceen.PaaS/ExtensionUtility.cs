using Ceen;

namespace Ceen.PaaS
{
    /// <summary>
    /// Basic helper methods
    /// </summary>
    public static class ExtensionUtility
    {
        /// <summary>
        /// Gets the remote IP for the currently executing request
        /// </summary>
        /// <returns>The current remote IP</returns>
        public static string RemoteIP { get => Context.Request?.GetRemoteIP(); }
    }
}