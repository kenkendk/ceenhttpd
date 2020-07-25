using System.Threading.Tasks;
using Ceen;
using Ceen.Database;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Handler that assigns the UserID if the user is logged in,
    /// and optionally rejects non-admin requests
    /// </summary>
    public class LoginChecker : Ceen.Security.Login.LoginRequiredHandler
    {
        /// <summary>
        /// If the login fails, we just clear the user ID
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns><c>false</c></returns>
        protected override bool SetLoginError(IHttpContext context)
        {
            context.Request.UserID = null;
            return false;
        } 

    }
}