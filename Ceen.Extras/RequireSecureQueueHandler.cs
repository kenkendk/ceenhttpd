using System;
using System.Threading.Tasks;

namespace Ceen.Extras
{
    /// <summary>
    /// Handler that checks the secure queue header is present
    /// </summary>
    public class RequireSecureQueueHandler : IHttpModule
    {
        /// <summary>
        /// The name of the queue to check for
        /// </summary>
        private readonly string m_queuename;

        /// <summary>
        /// Creates a new queue handler checker
        /// </summary>
        /// <param name="queuename">The name of the queue to check for</param>
        public RequireSecureQueueHandler(string queuename)
        {
            m_queuename = queuename ?? throw new ArgumentNullException(nameof(queuename));
        }

        /// <summary>
        /// Handles the request by checking for the secure queue header value
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <returns><c>true</c> if the request was rejected, <c>false</c> otherwise</returns>
        public Task<bool> HandleAsync(IHttpContext context)
        {
            var queue = QueueModule.GetQueue(m_queuename);
            if (queue == null || !queue.IsSecureRequest(context.Request))
            {
                context.Response.StatusCode = HttpStatusCode.Forbidden;
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
    }
}
