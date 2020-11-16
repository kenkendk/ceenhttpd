using System.Data;
using System.Threading.Tasks;
using Ceen.Database;

namespace Ceen.PaaS.Services
{
    public static class AdminHelper
    {
        /// <summary>
        /// Checks if the user is an admin
        /// </summary>
        /// <param name="db">The database connection</param>
        /// <param name="userid">The ID of the user to check</param>
        /// <returns>A flag indicating if the user is admin</returns>
        public static bool IsAdmin(this IDbConnection db, string userid)
        {
            return db.SelectCount<Database.UserGroupIndex>(x => x.UserID == userid && x.GroupID == IDConstants.AdminGroupID) > 0;
        }

        /// <summary>
        /// Checks if the user is an admin
        /// </summary>
        /// <param name="userid">The ID of the user to check</param>
        /// <returns>A flag indicating if the user is admin</returns>
        public static Task<bool> IsAdminAsync(string userid)
        {
            return DatabaseInstance.GetInstance().RunInTransactionAsync(db => db.IsAdmin(userid));
        }

        /// <summary>
        /// Checks if the user is an admin
        /// </summary>
        /// <param name="instance">The database instance to use</param>
        /// <param name="userid">The ID of the user to check</param>
        /// <returns>A flag indicating if the user is admin</returns>
        public static Task<bool> IsAdminAsync(DatabaseInstance instance, string userid)
        {
            return instance.RunInTransactionAsync(db => db.IsAdmin(userid));
        }
    }
}