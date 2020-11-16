using System.Threading.Tasks;
using Ceen;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Handler for the terms-of-service page
    /// </summary>
    public class TermsOfService : IHttpModule
    {
        /// <summary>
        /// The database instance
        /// </summary>
        protected readonly DatabaseInstance DB = DatabaseInstance.GetInstance();

        /// <summary>
        /// Handles a request for the terms-of-service document
        /// </summary>
        /// <param name="context">The http context</param>
        /// <returns><c>true</c></returns>
        public async Task<bool> HandleAsync(IHttpContext context)
        {
            var data = await Cache.TermsOfService.TryGetValueAsync();
            var encoding = System.Text.Encoding.UTF8;
            var contenttype = "text/html";

            if (data == null)
            {
                data = await DB.RunInTransactionAsync(db => Services.TextHelper.GetTextFromDb(db, TextConstants.TermsOfService, "en"));
                data = Services.MarkdownRenderer.RenderAsHtml((data ?? string.Empty));
                await Cache.TermsOfService.SetValueAsync(data);
            }

            await context.Response.WriteAllAsync(data, encoding, string.Format("{0}; charset={1}", contenttype, encoding.BodyName));

            return true;
        }
    }

    /// <summary>
    /// Handler for the privacy policy page
    /// </summary>
    public class PrivacyPolicy : IHttpModule
    {
        /// <summary>
        /// The database instance
        /// </summary>
        protected readonly DatabaseInstance DB = DatabaseInstance.GetInstance();

        /// <summary>
        /// Handles a request for the privacy policy document
        /// </summary>
        /// <param name="context">The http context</param>
        /// <returns><c>true</c></returns>
        public async Task<bool> HandleAsync(IHttpContext context)
        {
            var data = await Cache.PrivacyPolicy.TryGetValueAsync();
            var encoding = System.Text.Encoding.UTF8;
            var contenttype = "text/html";

            if (data == null)
            {
                data = await DB.RunInTransactionAsync(db => Services.TextHelper.GetTextFromDb(db, TextConstants.PrivacyPolicy, "en"));
                data = Services.MarkdownRenderer.RenderAsHtml((data ?? string.Empty));
                await Cache.PrivacyPolicy.SetValueAsync(data);
            }

            await context.Response.WriteAllAsync(data, encoding, string.Format("{0}; charset={1}", contenttype, encoding.BodyName));

            return true;
        }
    }    
}