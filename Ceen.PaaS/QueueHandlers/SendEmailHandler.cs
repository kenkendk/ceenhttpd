using System;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using System.Threading.Tasks;

namespace Ceen.PaaS.QueueHandlers
{
    /// <summary>
    /// The different types of email supported
    /// </summary>
    public enum SendEmailType
    {
        /// <summary>
        /// The email is something else
        /// </summary>
        Invalid,
        /// <summary>
        /// The email is a signup confirmation request
        /// </summary>
        Signup,
        /// <summary>
        /// The email is an activation notice
        /// </summary>
        Activation,
        /// <summary>
        /// The email is a password reset message
        /// </summary>
        PasswordReset,
        /// <summary>
        /// The email is an email address change confirmation request
        /// </summary>
        ChangeEmail
    }

    /// <summary>
    /// The request to send
    /// </summary>
    public class SendEmailRequest
    {
        /// <summary>
        /// The recipient email
        /// </summary>
        public string To;
        /// <summary>
        /// The display name to use
        /// </summary>
        public string Name;
        /// <summary>
        /// The locale to use
        /// </summary>
        public string Locale;
        /// <summary>
        /// The IP requesting the email
        /// </summary>
        public string RequestIP;
        /// <summary>
        /// The email type
        /// </summary>
        public SendEmailType Type;
        /// <summary>
        /// The ID causing the sending request
        /// </summary>
        public long ID;
    }

    /// <summary>
    /// Queue handler for emails
    /// </summary>
    public class SendEmailHandler : API.ControllerBase, IQueueV1
    {
        /// <summary>
        /// The main handler
        /// </summary>
        /// <param name="request">The send request</param>
        /// <returns>The result</returns>
        [HttpPost]
        public async Task<IResult> Index(SendEmailRequest request)
        {
            if (!Ceen.Extras.QueueModule.IsSecureRequest("emailqueue", Context.Request))
                return Forbidden;
            if (request == null)
                return BadRequest;

            switch (request.Type)
            {
                case SendEmailType.Signup:
                {
                    var code = await DB.RunInTransactionAsync(db => db.SelectItemById<Database.Signup.SignupEntry>(request.ID));
                    if (code == null)
                        return Status(BadRequest, "No entry with the given ID");
                    await Services.SendEmail.SignupEmail.SendAsync(request.Name, request.To, code.ActivationCode, request.Locale, request.RequestIP);
                    return OK;
                }

                case SendEmailType.Activation:
                {
                    var code = await DB.RunInTransactionAsync(db => db.SelectItemById<Database.ActivationRequest>(request.ID));
                    if (code == null)
                        return Status(BadRequest, "No entry with the given ID");
                    await Services.SendEmail.ActivationEmail.SendAsync(request.Name, request.To, code.Token, request.Locale, request.RequestIP);
                    return OK;
                }

                case SendEmailType.ChangeEmail:
                {
                    var code = await DB.RunInTransactionAsync(db => db.SelectItemById<Database.ChangeEmailRequest>(request.ID));
                    if (code == null)
                        return Status(BadRequest, "No entry with the given ID");

                    await Services.SendEmail.ChangeEmailRequest.SendAsync(request.Name, request.To, code.Token, request.Locale, request.RequestIP);
                    return OK;
                }

                case SendEmailType.PasswordReset:
                {
                    var code = await DB.RunInTransactionAsync(db => db.SelectItemById<Database.ResetPasswordRequest>(request.ID));
                        if (code == null)
                            return Status(BadRequest, "No entry with the given ID");

                    await Services.SendEmail.PasswordResetEmail.SendAsync(request.Name, request.To, code.Token, request.Locale, request.RequestIP);
                    return OK;
                }

                default:
                    return Status(BadRequest, $"Invalid request type: {request.Type}");
            }
        }

        
    }
}
