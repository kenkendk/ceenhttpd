using System;

namespace Ceen.PaaS
{
    /// <summary>
    /// Helper module to contain email settings
    /// </summary>
    public class EmailSignupSettings : IModuleWithSetup
    {
        /// <summary>
        /// Gets or sets the name of the email sender
        /// </summary>
        public string SenderName { get; set; }
        /// <summary>
        /// Gets ot sets the sender email address
        /// </summary>
        public string SenderEmail { get; set; }

        /// <summary>
        /// The email to send from
        /// </summary>
        public static string FromEmail 
            => LoaderContext.SingletonInstance<EmailSignupSettings>().SenderEmail;
        /// <summary>
        /// The email name to use for the sender
        /// </summary>
        public static string FromName
            => LoaderContext.SingletonInstance<EmailSignupSettings>().SenderName;

        /// <summary>
        /// Creates the signup settings
        /// </summary>
        public EmailSignupSettings()
        {
            LoaderContext.RegisterSingletonInstance(this);
        }

        /// <summary>
        /// Validates the module after it has been configured
        /// </summary>
        public void AfterConfigure()
        {
            if (string.IsNullOrWhiteSpace(SenderEmail))
                throw new Exception($"Missing {nameof(SenderEmail)} in {this.GetType()}");
            if (string.IsNullOrWhiteSpace(SenderName))
                throw new Exception($"Missing {nameof(SenderName)} in {this.GetType()}");
        }
    }
}