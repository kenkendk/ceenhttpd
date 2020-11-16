using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ceen.Security.Login;
using Ceen.Database;
using Ceen;
using static Ceen.Database.QueryUtil;

namespace Ceen.PaaS.Services
{
    public class LoginProviderMapper : Ceen.Security.Login.ILoginEntryModule
    {
        public Task AddLoginEntryAsync(LoginEntry record)
        {
            throw new NotImplementedException("Adding users is not supported");
        }

        public Task DropAllLoginEntriesAsync(string userid, string username)
        {
            throw new NotImplementedException("Deleting users is not supported");
        }

        public Task DropLoginEntryAsync(LoginEntry record)
        {
            throw new NotImplementedException("Deleting users is not supported");
        }

        public async Task<IEnumerable<LoginEntry>> GetLoginEntriesAsync(string username)
        {
            await Context.LogDebugAsync($"Attempting login for {username}");

            return await DatabaseInstance.GetInstance().RunInTransactionAsync(db =>
                db
                    // Find the users
                    .Select<Database.User>(x => 
                        !x.Disabled 
                        &&
                        string.Equals(x.Email, username, StringComparison.OrdinalIgnoreCase)
                    )
                    // Convert to authentication format
                    .Select(x => new LoginEntry() {
                        UserID = x.ID,
                        Username = x.Name,
                        Token = x.Password
                    })
                    // Force in-memory serialization
                    .ToArray()
                    // Return as enumerable (backed by array)
                    .AsEnumerable()
            );
        }

        public Task UpdateLoginTokenAsync(LoginEntry record)
        {
            throw new NotImplementedException("Updating passwords is not supported");
        }
    }
}