using System.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Ceen;
using Ceen.Database;

namespace Ceen.PaaS
{
    /// <summary>
    /// The database instance protecting the database
    /// </summary>
    public class DatabaseInstance : Ceen.Extras.DatabaseBackedModule
    {
        /// <summary>
        /// The types used in this instance
        /// </summary>
        protected override Type[] UsedTypes => new Type[] {
            typeof(Database.Signup.RequestToken),
            typeof(Database.Signup.SignupEntry),
            typeof(Database.ActivationRequest),
            typeof(Database.LoginHistory),
            typeof(Database.Notification),
            typeof(Database.SentEmailLog),
            typeof(Database.ResetPasswordRequest),
            typeof(Database.SentEmailLog),
            typeof(Database.User),
            typeof(Database.UserGroup),
            typeof(Database.UserGroupIndex),
            typeof(Database.TextEntry),
            typeof(Database.ImageMap)
        };

        /// <summary>
        /// Constructor
        /// </summary>
        public DatabaseInstance() 
        {
            LoaderContext.RegisterSingletonInstance(this);
        }

        /// <summary>
        /// Gets the database instance
        /// </summary>
        /// <returns>The database instance</returns>
        public static DatabaseInstance GetInstance() 
            => LoaderContext.SingletonInstance<DatabaseInstance>();

        /// <summary>
        /// Gets the table map for the given type
        /// </summary>
        /// <typeparam name="T">The type to get the map for</typeparam>
        /// <returns>The table mapping for the type</returns>
        public static TableMapping GetTypeMap<T>()
        {
            return GetInstance().GetDialect().GetTypeMap<T>();
        }

        /// <summary>
        /// Configures the database and sets it up
        /// </summary>
        public override void AfterConfigure()
        {
            base.AfterConfigure();

            // Wire up the Auth module
            new Ceen.Security.Login.LoginSettingsModule()
                .Authentication = new Services.LoginProviderMapper();

            // Set up built-in groups
            var groups = new Dictionary<string, string>()
            {
                { IDConstants.AdminGroupID, "Administrators" },
                { IDConstants.SqlAdminGroupID, "SQL Administrators" },
                { IDConstants.LogAdminGroupID, "Log Administrators" }
            };

            // The startup is always single threaded
            var db = m_con.UnguardedConnection;

            foreach (var n in groups)
            {
                try 
                {                    
                    m_con.UnguardedConnection.InsertOrIgnoreItem<Database.UserGroup>(new Database.UserGroup()
                    {
                        ID = n.Key,
                        Name = n.Value,
                    });
                }
                catch { }
            }

            // If there are no users, make sure we have at least one admin
            if (m_con.UnguardedConnection.SelectCount<Database.User>(new Empty()) == 0)
            {
                var username = SecretsHandler.GetSecret("DEFAULT_ADMIN_EMAIL");
                var password = SecretsHandler.GetSecret("DEFAULT_ADMIN_PASSWORD");
                var handle = SecretsHandler.GetSecret("DEFAULT_ADMIN_HANDLE");
                var displayname = SecretsHandler.GetSecret("DEFAULT_ADMIN_DISPLAYNAME");

                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    // If the user provided a clear-text password, create a hashed version
                    if (!password.StartsWith("PBKDF2$"))
                        password = Ceen.Security.PBKDF2.CreatePBKDF2(password);
                    if (string.IsNullOrWhiteSpace(handle))
                        handle = username.Split(new char[] {'@'}, 2).First();
                    if (string.IsNullOrWhiteSpace(displayname))
                        displayname = handle;

                    var u = m_con.UnguardedConnection.InsertItem(new Database.User() {
                        Handle = handle,
                        Name = displayname,
                        Email = username,
                        Password = password
                    });

                    m_con.UnguardedConnection.InsertItem(new Database.UserGroupIndex() {
                        GroupID = IDConstants.AdminGroupID,
                        UserID = u.ID
                    });
                }
            }
        }
    }
}
