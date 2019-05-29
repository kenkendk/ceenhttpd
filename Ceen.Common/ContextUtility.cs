using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        public static IHttpRequest Request => m_activeContext.Value?.Request;
        /// <summary>
        /// Gets the current active response
        /// </summary>
        public static IHttpResponse Response => m_activeContext.Value?.Response;

        /// <summary>
        /// Gets the current user ID
        /// </summary>
        public static string UserID => m_activeContext.Value?.Request.UserID;

        /// <summary>
        /// Gets the current active session, can be null if no session module is loaded
        /// </summary>
        public static IDictionary<string, string> Session => m_activeContext.Value?.Session;

        /// <summary>
        /// Gets the current request's log data
        /// </summary>
        public static IDictionary<string, string> LogData => m_activeContext.Value?.LogData;

        /// <summary>
        /// Logs a message
        /// </summary>
        /// <param name="level">The level to log</param>
        /// <param name="message">The message to log</param>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogMessageAsync(LogLevel level, string message, Exception ex)
        {
            return Current.LogMessageAsync(level, message, ex);
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogDebugAsync(string message, Exception ex = null)
        {
            return Current.LogDebugAsync(message, ex);
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogDebugAsync(Exception ex)
        {
            return Current.LogDebugAsync(null, ex);
        }        

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogErrorAsync(string message, Exception ex = null)
        {
            return Current.LogErrorAsync(message, ex);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogErrorAsync(Exception ex)
        {
            return Current.LogErrorAsync(null, ex);
        }

        /// <summary>
        /// Logs an information message
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogInformationAsync(string message, Exception ex = null)
        {
            return Current.LogInformationAsync(message, ex);
        }

        /// <summary>
        /// Logs an information message
        /// </summary>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogInformationAsync(Exception ex)
        {
            return Current.LogInformationAsync(null, ex);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogWarningAsync(string message, Exception ex = null)
        {
            return Current.LogWarningAsync(message, ex);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="ex">The exception to log</param>
        /// <returns>An awaitable task</returns>
        public static Task LogWarningAsync(Exception ex)
        {
            return Current.LogWarningAsync(null, ex);
        }
    }
}
