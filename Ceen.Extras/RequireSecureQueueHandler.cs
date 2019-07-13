using System;
using System.Threading.Tasks;

namespace Ceen.Extras
{
    /// <summary>
    /// Handler that checks the secure queue header is present
    /// </summary>
    public class RequireSecureQueueHandler : IHttpModule
    {
        private readonly string m_queuename;

        public RequireSecureQueueHandler(string queuename)
        {
            m_queuename = queuename ?? throw new ArgumentNullException(nameof(queuename));
        }

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
