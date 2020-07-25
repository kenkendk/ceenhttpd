namespace Ceen.PaaS
{
    /// <summary>
    /// Collection of constant string IDs
    /// </summary>
    public static class TextConstants
    {
        /// <summary>
        /// The landing page body
        /// </summary>
        public const string LandingPageContents = "Landing:Body";

        /// <summary>
        /// The terms of service contents
        /// </summary>
        public const string TermsOfService = "Legal:ToS";

        /// <summary>
        /// The privacy policy contents
        /// </summary>
        public const string PrivacyPolicy = "Legal:Privacy";

        /// <summary>
        /// The personal data policy contents
        /// </summary>
        public const string PersonDataPolicy = "Legal:GDPR";


        /// <summary>
        /// The body of the activation email
        /// </summary>
        public const string SignupConfirmationEmailBody = "Email:SignupConfirmation:Body";
        /// <summary>
        /// The subject of the activation email
        /// </summary>
        public const string SignupConfirmationEmailSubject = "Email:SignupConfirmation:Subject";

        /// <summary>
        /// The subject of the password reset text
        /// </summary>
        public const string ResetPasswordEmailSubject = "Email:PasswordReset:Subject";

        /// <summary>
        /// The body of the password reset email
        /// </summary>
        public const string ResetPasswordEmailBody = "Email:PasswordReset:Body";

        /// <summary>
        /// The subject line for the activation email
        /// </summary>
        public const string ActivationEmailSubject = "Email:Activated:Subject";
        /// <summary>
        /// The body of the activation email
        /// </summary>
        public const string ActivationEmailBody = "Email:Activated:Body";

        /// <summary>
        /// The email change request body
        /// </summary>
        public const string ChangeEmailRequestBody = "Email:ChangeEmail:Body";
        /// <summary>
        /// The email change request subject
        /// </summary>
        public const string ChangeEmailRequestSubject = "Email:ChangeEmail:Subject";

        /// <summary>
        /// The prefix to use for custom signup messages
        /// </summary>
        public const string SignupMessagesPrefix = "Signup:Code:";

    }
}