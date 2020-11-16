using System;
using System.Data;
using System.Threading.Tasks;
using Ceen.Database;

namespace Ceen.PaaS.Services.SendEmail
{
    public static class ChangeEmailRequest
    {
        /// <summary>
        /// Sends an activation request
        /// </summary>
        /// <param name="name">The recipient name</param>
        /// <param name="email">The recipient email</param>
        /// <param name="activationcode">The activation code</param>
        /// <param name="language">The language the message is sent in</param>
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
                Type = Database.EmailType.ActivationEmail,
                Delivered = false,
                RequestIP = requestIP
            };

            var markdown = string.Empty;
            var subject = string.Empty;

            // Insert the email log record
            await DatabaseInstance.GetInstance().RunInTransactionAsync(db =>
            {
                // Grab the subject and body at the same time
                markdown = Services.TextHelper.GetTextFromDb(db, TextConstants.ChangeEmailRequestBody, language);
                subject = Services.TextHelper.GetTextFromDb(db, TextConstants.ChangeEmailRequestSubject, language);

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