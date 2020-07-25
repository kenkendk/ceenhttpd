using System;
using System.Threading.Tasks;
using Ceen.Mvc;
using Ceen.Database;

namespace Ceen.PaaS.API
{
    public class ChangeEmailHandler : ControllerBase, IAPIv1
    {
        [HttpPost]
        public async Task<IResult> Activate(
            [Parameter(ParameterSource.Form)]
            string token
        )
        {
            if (string.IsNullOrWhiteSpace(token))
                return Status(BadRequest, "One or more fields missing");

            return await DB.RunInTransactionAsync(db =>
            {
                var req = db.SelectSingle<Database.ChangeEmailRequest>(x => x.Token == token);
                if (req == null)
                    return Status(Forbidden, "Invalid reset token");
                if (DateTime.Now - req.Created > TimeSpan.FromDays(2))
                    return Status(Forbidden, "Token has expired, please requests a new password reset");

                var user = db.SelectItemById<Database.User>(req.UserID);
                if (user == null)
                    return Status(Ceen.HttpStatusCode.InternalServerError, "User not found");

                user.Email = req.NewEmail;
                db.UpdateItem(user);
                db.DeleteItem(req);

                return OK;
            });
        }
    }
}