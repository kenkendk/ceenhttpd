using System.Threading.Tasks;
using Ceen;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Handler that requires the user to be admin
    /// </summary>
    public class AdminRequiredHandler : Ceen.Security.Login.LoginRequiredHandler
    {
        /// <summary>
        /// The database instance
        /// </summary>
        private readonly DatabaseInstance DB = DatabaseInstance.GetInstance();

        /// <summary>
        /// Handles the request
        /// </summary>
        /// <returns>The awaitable task.</returns>
        /// <param name="context">The requests context.</param>
        public override async Task<bool> HandleAsync(IHttpContext context)
        {
            var res = await base.HandleAsync(context);
            if (string.IsNullOrWhiteSpace(context.Request.UserID) || !await DB.RunInTransactionAsync(db => Services.AdminHelper.IsAdmin(db, context.Request.UserID)))
            {
                context.Request.UserID = null;
                context.Response.StatusCode = HttpStatusCode.Forbidden;
                return true;
            }

            return res;
        }
    }
}