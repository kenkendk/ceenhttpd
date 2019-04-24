using System;
using System.Collections.Generic;

namespace Ceen
{
    /// <summary>
    /// Helper class for providing the current execution context via the call context
    /// </summary>
    public static class Context
    {
        /// <summary>
        /// The scope data, using AsyncLocal
        /// </summary>
        private static readonly System.Threading.AsyncLocal<IHttpContext> m_activeContext = new System.Threading.AsyncLocal<IHttpContext>();

        /// <summary>
        /// Gets the current active context
        /// </summary>
        public static IHttpContext Current => m_activeContext.Value;

        /// <summary>
        /// Sets the current active context
        /// </summary>
        /// <param name="current">The context to set</param>
        public static void SetCurrentContext(IHttpContext current)
        {
            m_activeContext.Value = current;
        }

        /// <summary>
        /// Gets the current active request
        /// </summary>
        public static IHttpRequest CurrentRequest => m_activeContext.Value?.Request;
        /// <summary>
        /// Gets the current active response
        /// </summary>
        public static IHttpResponse CurrentResponse => m_activeContext.Value?.Response;

        /// <summary>
        /// Gets the current active session, can be null if no session module is loaded
        /// </summary>
        public static IDictionary<string, string> CurrentSession => m_activeContext.Value?.Session;

        /// <summary>
        /// Gets the current request's log data
        /// </summary>
        public static IDictionary<string, string> CurrentLogData => m_activeContext.Value?.LogData;

    }
}