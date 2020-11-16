using System;

namespace Ceen.PaaS.Services
{
    public static class PasswordPolicy
    {
        /// <summary>
        /// The random number generator to use
        /// </summary>
        private static readonly Random _rnd = new Random();

        /// <summary>
        /// The lock guarding the random number generator
        /// </summary>
        private static readonly object _rndlock = new object();

        /// <summary>
        /// Enforces the password policy
        /// </summary>
        /// <param name="password">The password to validate</param>
        public static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                throw new Ceen.HttpException(Ceen.HttpStatusCode.BadRequest, "Password must be at least 8 characters");
        }

        /// <summary>
        /// Generates an activation code
        /// </summary>
        /// <returns>The activation code</returns>
        public static string GenerateActivationCode()
        {
            // We do not use a PRNG because the code is not used for encryption
            // and we do not have a high security standard for the activation
            // codes.
            byte[] buf;
            lock(_rndlock)
            {
                buf = new byte[_rnd.Next(20, 28)];
                _rnd.NextBytes(buf);
            }

            // Make it URL-safe
            return Convert
                .ToBase64String(buf)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_'); ;
        }

        /// <summary>
        /// Basic email format validation
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex EMAIL_VALIDATOR = new System.Text.RegularExpressions.Regex(@"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$");

        /// <summary>
        /// Does a rudimentary check for the email format
        /// </summary>
        /// <param name="email">The string to validate</param>
        /// <returns><c>true</c> if the email looks valid; <c>false</c> otherwise</returns>
        public static bool IsValidEmail(string email)
        {
            try
            {
                // Use the built-in .Net email address validation
                return email == new System.Net.Mail.MailAddress(email).Address;
            }
            catch
            {
                return false;
            }
        }        

    }
}
