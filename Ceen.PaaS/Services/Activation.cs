using System;
using System.Threading.Tasks;
using Ceen.Database;

namespace Ceen.PaaS.Services
{
    /// <summary>
    /// Helper class to activate a signup request
    /// </summary>
    public static class Activation
    {
        /// <summary>
        /// Activates a user
        /// </summary>
        /// <param name="id">The signup request ID</param>
        /// <returns>An awaitable task</returns>
        internal static async Task ActivateUser(string id)
        {
            // Create the activation request
            var req = await DatabaseInstance.GetInstance().RunInTransactionAsync(db => {
                var sr = db.SelectItemById<Database.Signup.SignupEntry>(id);
                if (sr == null)
                    throw new ArgumentException($"No such signup request: {id}");

                var ar = db.SelectSingle<Database.ActivationRequest>(x => x.SignupID == id);
                if (ar != null)
                {
                    if (DateTime.Now - ar.LastSent < TimeSpan.FromMinutes(15))
                    {
                        Ceen.Context.LogInformationAsync($"Not sending new activation email to {id} as the previous one was sent {ar.LastSent}");
                        return null;
                    }
                    else
                    {
                        ar.LastSent = DateTime.Now;
                        db.UpdateItem(ar);
                    }
                }
                else
                {
                    ar = db.InsertItem(new Database.ActivationRequest() {
                        SignupID = id,
                        Token = Services.PasswordPolicy.GenerateActivationCode(),
                        LastSent = DateTime.Now
                    });
                }

                return new {
                    Name = sr.Name,
                    Email = sr.Email,
                    ID = ar.ID,
                    Locale = sr.Locale
                };
            });

            if (req == null)
                return;

            await Queues.SendActivationEmailAsync(req.Name, req.Email, req.ID, req.Locale);
        }
    }
}
