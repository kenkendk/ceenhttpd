using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Extras;

namespace Ceen.PaaS
{
    public static class Queues
    {
        private static string GetMatchingLocale()
        {
            var rq = Context.Request;
            if (rq == null)
                return null;

            return Services.LocaleHelper.GetBestLocale(rq);
        }

        private static readonly QueueModule.QueueHelperFixedUrl<QueueHandlers.SendEmailRequest> SendEmail 
            = new QueueModule.QueueHelperFixedUrl<QueueHandlers.SendEmailRequest>(
                "emailqueue", "/queue/v1/sendemail"
            );

        public static Task SendEmailChangeConfirmationEmailAsync(string name, string to, int id, string locale)
            => SendEmail.SubmitJobAsync(new QueueHandlers.SendEmailRequest() {
                Type = QueueHandlers.SendEmailType.ChangeEmail,
                Name = name,
                To = to,
                ID = id,
                Locale = GetMatchingLocale(),
                RequestIP = ExtensionUtility.RemoteIP
            });

        public static Task SendSignupConfirmationEmailAsync(string name, string to, int id, string locale)
            => SendEmail.SubmitJobAsync(new QueueHandlers.SendEmailRequest()
            {
                Type = QueueHandlers.SendEmailType.Signup,
                Name = name,
                To = to,
                ID = id,
                Locale = GetMatchingLocale(),
                RequestIP = ExtensionUtility.RemoteIP
            });

        public static Task SendPasswordResetEmailAsync(string name, string to, int id, string locale)
            => SendEmail.SubmitJobAsync(new QueueHandlers.SendEmailRequest()
            {
                Type = QueueHandlers.SendEmailType.PasswordReset,
                Name = name,
                To = to,
                ID = id,
                Locale = GetMatchingLocale(),
                RequestIP = ExtensionUtility.RemoteIP
            });

        public static Task SendActivationEmailAsync(string name, string to, int id, string locale)
            => SendEmail.SubmitJobAsync(new QueueHandlers.SendEmailRequest()
            {
                Type = QueueHandlers.SendEmailType.Activation,
                Name = name,
                To = to,
                ID = id,
                Locale = GetMatchingLocale(),
                RequestIP = ExtensionUtility.RemoteIP
            });

    }
}
