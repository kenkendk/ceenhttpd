using System;
using System.Data;
using System.Threading.Tasks;
using Ceen.Database;

namespace Ceen.PaaS.Services.SendEmail
{
    /// <summary>
    /// Wrapper for sending signup emails 
    /// </summary>
    public static class SignupEmail
    {
        // TODO: Use loader context, and allow configuration

        /// <summary>
        /// The time to look back when detecting multiple signup requests from the same IP
        /// </summary>
        private static readonly TimeSpan LOOKBACK_PERIOD = TimeSpan.FromHours(1);

        /// <summary>
        /// The maximum number of requests allowed from one IP in the lookback period
        /// </summary>
        private static readonly long MAX_REQUESTS_FROM_SAME_IP = 5;

        /// <summary>
        /// The lock used to prevent racing the sent email log checker
        /// </summary>
        private static readonly Ceen.AsyncLock SIGNUP_EMAIL_LOCK = new Ceen.AsyncLock();


        /// <summary>
        /// Checks if the current IP has violated the IP rate limit for the current period
        /// </summary>
        /// <returns>A flag indicating if the rate limit has been violated</returns>
        public static async Task<bool> ViolatesIPRateLimit() => await GetSignupEmailsSentByIPAsync() > MAX_REQUESTS_FROM_SAME_IP;

        /// <summary>
        /// Gets the number of signup emails sent by the current requester within the given period
        /// </summary>
        /// <returns>The count</returns>
        private static async Task<long> GetSignupEmailsSentByIPAsync()
        {
            if (DatabaseInstance.Current == null)
                return await DatabaseInstance.GetInstance().RunInTransactionAsync(db => GetSignupEmailsSentByIPAsync());

            var requestIP = ExtensionUtility.RemoteIP;
            return DatabaseInstance.Current.SelectCount<Database.SentEmailLog>(
                x => 
                (x.When > DateTime.Now - LOOKBACK_PERIOD)
                && (x.Type == Database.EmailType.SignupConfirmation)
                && (x.Delivered == true)
                && (x.RequestIP == requestIP)
            );
        }

        /// <summary>
        /// Sends an signup email if the request appears legitimate
        /// </summary>
        /// <param name="name">The name of the recipient</param>
        /// <param name="email">The email of the recipient</param>
        /// <param name="activationcode">The activation code to include in the email</param>
        /// <param name="language">The language to use for the email</param>
        /// <param name="requestIP">The IP requesting the signup email</param>
        /// <returns>An awaitable task</returns>
        public static async Task SendAsync(string name, string email, string activationcode, string language, string requestIP)
        {
            if (DatabaseInstance.Current != null)
                throw new InvalidOperationException("Cannot send email from within a transaction scope");

            var logEntry = new Database.SentEmailLog()
            {
                From = EmailSignupSettings.FromEmail,
                To = email,
                When = DateTime.Now,
                Type = Database.EmailType.SignupConfirmation,
                Delivered = false,
                RequestIP = requestIP
            };

            using(await SIGNUP_EMAIL_LOCK.LockAsync())
            {
                if (await ViolatesIPRateLimit())
                    throw new Exception("Too many emails from same IP");

                var markdown = string.Empty;
                var subject = string.Empty;

                // Insert the email log record
                await DatabaseInstance.GetInstance().RunInTransactionAsync(db => {
                    // Grab the subject and body at the same time
                    markdown = Services.TextHelper.GetTextFromDb(db, TextConstants.SignupConfirmationEmailBody, language);
                    subject = Services.TextHelper.GetTextFromDb(db, TextConstants.SignupConfirmationEmailSubject, language);

                    if (string.IsNullOrWhiteSpace(markdown))
                        throw new DataException("Database is missing a template for the email body");
                    if (string.IsNullOrWhiteSpace(subject))
                        throw new DataException("Database is missing a template for the email subject");

                    markdown = BasicTemplating.ReplaceInTemplate(markdown, new {
                        activationcode,
                        username = name
                    });

                    subject = BasicTemplating.ReplaceInTemplate(subject, new {
                        activationcode,
                        username = name
                    });

                    logEntry.Subject = subject;
                    db.InsertItem(logEntry);
                });

                var text = MarkdownRenderer.RenderAsText(markdown);
                markdown = MarkdownRenderer.RenderAsHtml(markdown);

                // Deliver the email to the server
                await SparkPost.SendEmailAsync(SparkPost.Transmission.Create(
                    EmailSignupSettings.FromName,
                    EmailSignupSettings.FromEmail,
                    name,
                    email,
                    subject,
                    markdown,
                    text,
                    true
                ));

                // Record that we succeeded delivering the email to the server
                logEntry.Delivered = true;
                await DatabaseInstance.GetInstance().RunInTransactionAsync(db => db.UpdateItem(logEntry));
            }
        }
    }
}