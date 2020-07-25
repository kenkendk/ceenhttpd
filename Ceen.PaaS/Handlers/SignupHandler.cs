using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace Ceen.PaaS.API
{
    /// <summary>
    /// Handler class for the signup page
    /// </summary>
    public class SignupHandler : ControllerBase, IAPIv1
    {
        /// <summary>
        /// The error codes for signup
        /// </summary>
        public enum StatusCode
        {
            /// <summary>The signup was a success, wait for the activation email</summary>
            [Code("SUCCESS")]
            [Description(null, "We received your signup. We sent you an email with a confirmation link. You must click the link within 24 hours to confirm the signup. If you do not see the email in your inbox, please check your spam folder.")]
            Success,
            /// <summary>One or more fields were invalid or missing</summary>
            [Code("INVALID_FIELD")]
            [Description(null, "One or more fields do not have valid values")]
            InvalidField,
            /// <summary>The supplied token was invalid</summary>
            [Code("INVALID_TOKEN")]
            [Description(null, "Your browser did not send a valid message. Reload the page and try again. You must have javascript activated in your browser to use our signup form.")]
            InvalidToken,
            /// <summary>The supplied token was valid, but cannot be used yet</summary>
            [Code("TOO_FAST")]
            [Description(null, "You typed very fast, please wait a few seconds and try again")]
            TooFastInput,
            /// <summary>The account is already signed up</summary>
            [Code("ALREADY_ACTIVE")]
            [Description(null, "You are already signed up and have confirmed it.")]
            AlreadyActivated,
            /// <summary>A new activation email was sent</summary>
            [Code("CHECK_EMAIL")]
            [Description(null, "We sent you a new activation email, please check you spam folder and make sure you click the activation link within 24 hours.")]
            SentActivationEmail,
            /// <summary>An activation email is already on the way</summary>
            [Code("WAIT_FOR_EMAIL")]
            [Description(null, "We recently sent you an activation email. Please wait a few minutes for the email to arrive and be sure to check your spam folder.")]
            WaitForActivationEmail,

            [Code("TOO_MANY_REQUESTS")]
            [Description(null, "Too many signup requests from this IP. Please wait an hour and try again.")]
            TooManyRequestFromIp,
        }

        /// <summary>
        /// Attempts to locate a localized message, and returns the default message if none is found
        /// </summary>
        /// <param name="code">The status code to respond</param>
        /// <param name="language">The preferred user language</param>
        /// <param name="fieldname">The field to report the error in, if any</param>
        /// <returns>A result</returns>
        private Task<SignupResult> GetTranslatedMessageAsync(StatusCode code, string language, string fieldname = null)
            // TODO: Consider a cache for these
            => DB.RunInTransactionAsync(db =>
                GetTranslatedMessage(db, code, language, fieldname)
            );

        /// <summary>
        /// Attempts to locate a localized message, and returns the default message if none is found
        /// </summary>
        /// <param name="db">The database instance to use</param>
        /// <param name="code">The status code to respond</param>
        /// <param name="language">The preferred user language</param>
        /// <param name="fieldname">The field to report the error in, if any</param>
        /// <returns>A result</returns>
        private SignupResult GetTranslatedMessage(System.Data.IDbConnection db, StatusCode code, string language, string fieldname = null)
        {
            // TODO: Consider a cache for these
            var res = Services.TextHelper.GetTextFromDb(db, TextConstants.SignupMessagesPrefix + code.ToString(), language);
            return new SignupResult(code, language, fieldname, res);
        }

        /// <summary>
        /// The class for reporting a signup result
        /// </summary>
        public class SignupResult : SignalResponseBase<StatusCode>
        {
            /// <summary>
            /// The field with an error, if any
            /// </summary>
            public readonly string Fieldname;

            /// <summary>
            /// Constructs a new signup result class
            /// </summary>
            /// <param name="code">The status code to use</param>
            /// <param name="language">The language to use</param>
            /// <param name="fieldname">The field with an error, if any</param>
            /// <param name="overridemessage">The overridemessage, if any</param>
            public SignupResult(StatusCode code, string language, string fieldname, string overridemessage)
                : base(code, language, overridemessage)
            {
                Fieldname = fieldname;
            }
        }

        // TODO: Use loader context, and allow configuration

        /// <summary>
        /// The minimum time the user is expected to use when entering their information
        /// </summary>
        public static readonly TimeSpan MIN_INPUT_TIME = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The minimum time the user must wait before we re-send the activation email
        /// </summary>
        public static readonly TimeSpan MIN_EMAIL_TIME = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The maximum time a token is valid
        /// </summary>
        public static readonly TimeSpan MAX_TOKEN_LIFETIME = TimeSpan.FromDays(1);

        /// <summary>
        /// Creates a token for signing up
        /// </summary>
        [HttpPost]
        public async Task<IResult> Create()
        {
            var rec = new Database.Signup.RequestToken() { 
                WhenCreated = DateTime.Now 
            };

            await DB.RunInTransactionAsync(db => {                
                db.InsertItem(rec);
            });

            return Json(new { Token = rec.ID });
        }

        /// <summary>
        /// Handles a confirmation request
        /// </summary>
        /// <param name="code">The activation code</param>
        [HttpPost]
        public Task<IResult> Confirm(string code)
        {
            return DB.RunInTransactionAsync(db =>
            {
                var entry = db.SelectSingle<Database.Signup.SignupEntry>(x => x.ActivationCode == code);
                if (entry == null)
                    return Status(Forbidden, "Invalid token");

                if (DateTime.Now - entry.When > MAX_TOKEN_LIFETIME)
                    return Status(BadRequest, "Token exists but is no longer valid, please sign up again");

                entry.Status = Database.Signup.SignupStatus.Confirmed;
                db.UpdateItem(entry);
                return OK;
            });
        }

        /// <summary>
        /// The request from the client for a signup
        /// </summary>
        public class SignupData
        {
            /// <summary>
            /// The name to use
            /// </summary>
            public string Name;
            /// <summary>
            /// The email to use
            /// </summary>
            public string Email;
            /// <summary>
            /// The token to use
            /// </summary>
            public string Token;
        }

        /// <summary>
        /// Handles a signup request
        /// </summary>
        [HttpPost]
        public async Task<IResult> Index(SignupData data)
        {
            var language = Services.LocaleHelper.GetBestLocale(Context.Request);

            if (string.IsNullOrWhiteSpace(data.Name))
                return await GetTranslatedMessageAsync(StatusCode.InvalidField, language, "name");
            if (string.IsNullOrWhiteSpace(data.Email))
                return await GetTranslatedMessageAsync(StatusCode.InvalidField, language, "email");
            if (string.IsNullOrWhiteSpace(data.Token))
                return await GetTranslatedMessageAsync(StatusCode.InvalidToken, language);
            if (!Services.PasswordPolicy.IsValidEmail(data.Email))
                return await GetTranslatedMessageAsync(StatusCode.InvalidField, language, "email");

            Database.Signup.SignupEntry entry = null;

            var res = await DB.RunInTransactionAsync(async db => {

                var item = db.SelectItemById<Database.Signup.RequestToken>(data.Token);
                if (item == null)
                    return GetTranslatedMessage(db, StatusCode.InvalidToken, language);
                if ((DateTime.Now - item.WhenCreated) < MIN_INPUT_TIME)
                    return GetTranslatedMessage(db, StatusCode.TooFastInput, language);

                // Delete this instance
                db.DeleteItem(item);

                // And delete everything older than 1h
                var deleteLimit = DateTime.Now - TimeSpan.FromHours(1);
                db.Delete<Database.Signup.RequestToken>(x => x.WhenCreated < deleteLimit);

                entry = db.SelectSingle<Database.Signup.SignupEntry>(x => x.Email == data.Email);
                if (entry != null)
                {
                    if (entry.Status == Database.Signup.SignupStatus.Activated || entry.Status == Database.Signup.SignupStatus.Confirmed)
                        return GetTranslatedMessage(db, StatusCode.AlreadyActivated, language);

                    var last_sent = db.SelectSingle<Database.SentEmailLog>(x => x.To == entry.Email);
                    if (last_sent != null && (DateTime.Now - last_sent.When) < MIN_EMAIL_TIME)
                        return GetTranslatedMessage(db, StatusCode.WaitForActivationEmail, language);

                    if (await Services.SendEmail.SignupEmail.ViolatesIPRateLimit())
                        return GetTranslatedMessage(db, StatusCode.TooManyRequestFromIp, language);

                    // Create a new code, to avoid attacks based on the stale code
                    entry.ActivationCode = Services.PasswordPolicy.GenerateActivationCode();
                    entry.LastAttempt = DateTime.Now;
                    db.UpdateItem(entry);

                    return GetTranslatedMessage(db, StatusCode.SentActivationEmail, language);
                }

                db.InsertItem(entry = new Database.Signup.SignupEntry()
                {
                    Email = data.Email,
                    Name = data.Name,
                    ActivationCode = Services.PasswordPolicy.GenerateActivationCode(),
                    Status = Database.Signup.SignupStatus.Created,
                    LastAttempt = DateTime.Now,
                    Locale = language
                });

                return GetTranslatedMessage(db, StatusCode.Success, language);
            });

            // Queue the sending without hogging the database lock
            if ((res.CodeValue == StatusCode.Success) || (res.CodeValue == StatusCode.SentActivationEmail))
                await Queues.SendSignupConfirmationEmailAsync(entry.Name, entry.Email, entry.ID, language);

            return res;
        }
    }


}