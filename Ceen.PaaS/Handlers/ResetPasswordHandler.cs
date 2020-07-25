using System;
using System.Linq;
using System.Threading.Tasks;
using Ceen.Mvc;
using Ceen.Database;

namespace Ceen.PaaS.API
{
    public class ResetPasswordHandler : ControllerBase, IAPIv1
    {
        [HttpPost]
        public async Task<IResult> PreApprove(
            [Parameter(ParameterSource.Form)]
            string activationCode
        )
        {
            if (string.IsNullOrWhiteSpace(activationCode))
                return Status(BadRequest, "One or more fields missing");

            return await DB.RunInTransactionAsync(db =>
            {
                var req = db.SelectSingle<Database.ResetPasswordRequest>(x => x.Token == activationCode);
                if (req == null)
                    return Status(Forbidden, "Invalid reset token");
                if (DateTime.Now - req.Created > TimeSpan.FromDays(2))
                    return Status(Forbidden, "Token has expired, please requests a new password reset");

                var user = db.SelectItemById<Database.User>(req.UserID);
                if (user == null)
                    return Status(Ceen.HttpStatusCode.InternalServerError, "User not found");

                return OK;
            });
        }

        [HttpPost]
        public async Task<IResult> Activate(
            [Parameter(ParameterSource.Form)]
            string activationCode,
            [Parameter(ParameterSource.Form)]
            string password,
            [Parameter(ParameterSource.Form)]
            string repeated)
        {
            if (new [] { activationCode, password, repeated }.Any(x => string.IsNullOrWhiteSpace(x)))
                return Status(BadRequest, "One or more fields missing");
            if (password != repeated)
                return Status(BadRequest, "The new password does not match the repeated one");

            Services.PasswordPolicy.ValidatePassword(password);

            return await DB.RunInTransactionAsync(db => {
                var req = db.SelectSingle<Database.ResetPasswordRequest>(x => x.Token == activationCode);
                if (req == null)
                    return Status(Forbidden, "Invalid reset token");
                if (DateTime.Now - req.Created > TimeSpan.FromDays(2))
                    return Status(Forbidden, "Token has expired, please requests a new password reset");

                var user = db.SelectItemById<Database.User>(req.UserID);
                if (user == null)
                    return Status(Ceen.HttpStatusCode.InternalServerError, "User not found");

                user.Password = Ceen.Security.PBKDF2.CreatePBKDF2(password);
                db.UpdateItem(user);
                db.DeleteItem(req);


                return OK;
            });
        }

        [HttpPost]
        public async Task<IResult> Request(
            [Parameter(ParameterSource.Form)]
            string email
        )
        {
            if (string.IsNullOrWhiteSpace(email))
                return Status(BadRequest, "Missing email field");

            // Try to not leak information about existence of email address
            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(600, 1500)));

            Database.User user = null;
            var rr = await DB.RunInTransactionAsync(db => {
                // TODO: Abort if we have multiple accounts with the same email
                user = db.SelectSingle<Database.User>(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
                if (user != null) 
                {
                    var resetreq = db.SelectSingle<Database.ResetPasswordRequest>(x => x.UserID == user.ID);
                    if (resetreq != null)
                    {
                        if ((DateTime.Now - resetreq.Created) > TimeSpan.FromDays(1))
                        {
                            resetreq.Created = DateTime.Now;
                            resetreq.Token = Services.PasswordPolicy.GenerateActivationCode();                            
                            db.UpdateItem(resetreq);

                            return resetreq;
                        }
                    }
                    else
                    {
                        db.InsertItem(resetreq = new Database.ResetPasswordRequest() {
                            UserID = user.ID,
                            Token = Services.PasswordPolicy.GenerateActivationCode()
                        });

                        return resetreq;
                    }
                }

                return null;
            });

            if (user != null && rr != null)
                await Queues.SendPasswordResetEmailAsync(user.Name, user.Email, rr.ID, Services.LocaleHelper.GetBestLocale(Ceen.Context.Request));

            return OK;
        }

    }
}
