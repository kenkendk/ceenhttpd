using System.Collections.Generic;
using System;
using Ceen.Mvc;
using Ceen.Database;
using System.Threading.Tasks;
using Ceen;
using Newtonsoft.Json;
using System.Data;
using System.Linq;

namespace Ceen.PaaS.API
{
    public class UserHandler : ControllerBase, IAPIv1
    {
        /// <summary>
        /// Layout for the quick response
        /// </summary>
        private class QuickResponse
        {
            /// <summary>
            /// A value indicating if the user is logged in
            /// </summary>
            [JsonProperty("loggedIn")]
            public bool LoggedIn;
            /// <summary>
            /// The user ID
            /// </summary>
            [JsonProperty("userID")]
            public string UserID;
            /// <summary>
            /// The display name of the user
            /// </summary>
            [JsonProperty("name")]
            public string Name;
            /// <summary>
            /// The display name of the user
            /// </summary>
            [JsonProperty("imageUrl")]
            public string AvatarImage;
            /// <summary>
            /// A value indicating if the user has admin rights
            /// </summary>
            [JsonProperty("isAdmin")]
            public bool IsAdmin;
            /// <summary>
            /// The number of unread notifications for the user
            /// </summary>
            [JsonProperty("unreadNotifications")]
            public long UnreadNotifications;
        }

        /// <summary>
        /// Information when updating the password
        /// </summary>
        public class UpdatePasswordInfo
        {
            /// <summary>
            /// The current password for the user
            /// </summary>
            public string Current;
            /// <summary>
            /// The new password for the user
            /// </summary>
            public string New;
            /// <summary>
            /// The repeated new password
            /// </summary>
            public string Repeated;
        }

        /// <summary>
        /// Gets a value indicating if the <paramref name="userid" /> is for the current user
        /// </summary>
        /// <param name="userid">The userID to test</param>
        /// <returns><c>true</c> if the user ID refers to the current logged in user; <c>false</c> otherwise</returns>
        protected bool IsSelfUser(string userid) => !string.IsNullOrWhiteSpace(Context.UserID) && (Context.UserID == userid || userid == "me");

        /// <summary>
        /// Handles the quick info call
        /// </summary>
        /// <param name="userid">The user to query</param>
        /// <returns>A response item</returns>
        [HttpGet]
        [Route("{userid}/quick")]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> GetQuick(string userid)
        {
            // If the user is not logged in, send empty response
            if (string.IsNullOrWhiteSpace(Context.UserID))
                return Json(new QuickResponse());

            // Only allow queries to own data
            if (!IsSelfUser(userid))
                return Forbidden;
            
            return Json(await DB.RunInTransactionAsync(db => LoadQuickInfo(db)));
        }

        /// <summary>
        /// Gets an object representing an anonymous user
        /// </summary>
        public static object AnonymousQuickInfo => new QuickResponse();

        /// <summary>
        /// Returns the quick-info object for json serialization
        /// </summary>
        /// <param name="db">The connection to use</param>
        /// <returns>The user info object</returns>
        public static object LoadQuickInfo(IDbConnection db)
        {
            var uid = Context.UserID;
            // If the user is not logged in, send empty response
            if (string.IsNullOrWhiteSpace(uid))
                return new QuickResponse();

            // Build a response
            var res = new QuickResponse()
            {
                LoggedIn = true,
                UserID = uid
            };

            // Add database information
            var user = db.SelectItemById<Database.User>(uid);
            res.Name = user.Name;
            res.AvatarImage = Services.Images.CreateLinkForId(user.AvatarImageID);
            res.UnreadNotifications = db.SelectCount<Database.Notification>(x => x.UserID == uid && x.Seen == false);
            res.IsAdmin = Services.AdminHelper.IsAdmin(db, uid);

            return res;
        }

        /// <summary>
        /// Class with display settings for a user
        /// </summary>
        public class UserSettings
        {
            /// <summary>
            /// The user ID
            /// </summary>
            public string ID;
            /// <summary>
            /// The users display name
            /// </summary>
            public string Handle;
            /// <summary>
            /// The users profile picture
            /// </summary>
            public string Avatar;
        }

        /// <summary>
        /// The settings for the current user
        /// </summary>
        public class OwnUserSettings : UserSettings
        {
            /// <summary>
            /// The users email
            /// </summary>
            public string Email;
            /// <summary>
            /// The users name
            /// </summary>
            public string Name;
            /// <summary>
            /// The users invoice address
            /// </summary>
            public string InvoiceAddress;
            /// <summary>
            /// The users delivery address
            /// </summary>
            public string DeliveryAddress;
            /// <summary>
            /// A flag indicating if the user is disabled
            /// </summary>
            public bool? Disabled;
            /// <summary>
            /// A flag indicating if the user is an admin
            /// </summary>
            public bool? Admin;
            /// <summary>
            /// A flag indicating if two-factor authentication is required
            /// </summary>
            public bool? Require2FA;
        }

        [HttpGet]
        [Route("{userid}")]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> Get(string userid)
        {
            // Only logged in users can query user information
            if (string.IsNullOrWhiteSpace(Context.UserID))
                return Forbidden;

                var res = IsSelfUser(userid)
                    ? new OwnUserSettings() 
                    : new UserSettings();

            await DB.RunInTransactionAsync(db => {
                var user = db.SelectItemById<Database.User>(Context.UserID);
                res.ID = user.ID;
                res.Handle = user.Handle;
                res.Avatar = Services.Images.CreateLinkForId(user.AvatarImageID);

                if (res is OwnUserSettings ous)
                {
                    ous.Email = user.Email;
                    ous.Name = user.Name;
                    ous.InvoiceAddress = user.InvoiceAddress;
                    ous.DeliveryAddress = user.DeliveryAddress;
                }
            });

            return Json(res);
        }

        [HttpPatch]
        [Ceen.Mvc.Name("index")]
        public Task<IResult> Patch(OwnUserSettings settings)
        {
            // The normal user can only update their own settings
            return UpdateUserAsync(Context.UserID, settings);
        }

        /// <summary>
        /// Updates the user settings
        /// </summary>
        /// <param name="userid">The user to update</param>
        /// <param name="settings">The settings to apply</param>
        /// <returns>The request result</returns>
        protected async Task<IResult> UpdateUserAsync(string userid, OwnUserSettings settings)
        {
            var requestuserid = Context.UserID;
            // Only allow calls by logged in users
            if (string.IsNullOrWhiteSpace(requestuserid))
                return Forbidden;
            if (string.IsNullOrWhiteSpace(userid))
                return BadRequest;

            var isself = IsSelfUser(userid);

            if (settings == null)
                return Status(BadRequest, "Missing update information");

            try
            {
                Database.ChangeEmailRequest nx = null;
                Database.User user = null;
                string oldemail = null;
                var res = await DB.RunInTransactionAsync(db =>
                {
                    var isadmin = Services.AdminHelper.IsAdmin(db, requestuserid);
                    if (!isself && !isadmin)
                        return Forbidden;

                    user = db.SelectItemById<Database.User>(userid);
                    if (!string.IsNullOrWhiteSpace(settings.Handle))
                        user.Handle = settings.Handle;
                    if (!string.IsNullOrWhiteSpace(settings.Name))
                        user.Name = settings.Name;
                    if (settings.InvoiceAddress != null)
                        user.InvoiceAddress = settings.InvoiceAddress;
                    if (settings.DeliveryAddress != null)
                        user.DeliveryAddress = settings.DeliveryAddress;

                    if (isadmin && settings.Disabled != null)
                        user.Disabled = settings.Disabled.Value;
                    if (isadmin && settings.Require2FA != null)
                        user.Require2FA = settings.Require2FA.Value;

                    // Register a new activation request
                    if (!string.IsNullOrWhiteSpace(settings.Email) && settings.Email != user.Email)
                    {                        
                        if (!Services.PasswordPolicy.IsValidEmail(settings.Email))
                            return Status(BadRequest, "The new email address is not valid");

                        oldemail = user.Email;
                        if (isadmin)
                        {
                            user.Email = settings.Email;                            
                        }
                        else
                        {                        
                            db.InsertItem(nx = new Database.ChangeEmailRequest()
                            {
                                UserID = user.ID,
                                NewEmail = settings.Email,
                                Token = Services.PasswordPolicy.GenerateActivationCode()
                            });
                        }
                    }                    

                    //TODO: The profile image?


                    db.UpdateItem(user);

                    // Toggle admin status
                    if (isadmin && settings.Admin != null)
                    {
                        if (settings.Admin.Value)
                        {
                            db.InsertOrIgnoreItem(new Database.UserGroupIndex() {
                                UserID = user.ID,
                                GroupID = IDConstants.AdminGroupID
                            });
                        }
                        else
                        {
                            db.Delete<Database.UserGroupIndex>(x =>
                                x.UserID == user.ID
                                &&
                                x.GroupID == IDConstants.AdminGroupID
                            );
                        }
                    }
                    if (isadmin && !string.IsNullOrWhiteSpace(oldemail))
                    {
                        //TODO: Implement the change email notification
                        //Services.SendEmail.ChangeEmailNotification(oldemail, settings.Email);
                    }

                    return OK;
                });

                if (user != null && nx != null)
                    await Queues.SendEmailChangeConfirmationEmailAsync(user.Name, nx.NewEmail, nx.ID, Services.LocaleHelper.GetBestLocale(Context.Request));

                return res;
            }
            catch (Exception ex)
            {
                var t = Ceen.Extras.CRUDExceptionHelper.WrapExceptionMessage(ex);
                if (t != null)
                    return t;

                throw;
            }
        }

        protected async Task<IResult> UpdatePasswordAsync(string userid, UpdatePasswordInfo req)
        {
            var requestuserid = Context.UserID;
            // Only allow calls by logged in users
            if (string.IsNullOrWhiteSpace(requestuserid))
                return Forbidden;

            if (new [] { req?.Current, req?.New, req?.Repeated }.Any(x => string.IsNullOrWhiteSpace(x)))
                return Status(BadRequest, "One or more fields are missing");
            if (req.New != req.Repeated)
                return Status(BadRequest, "The new password does not match the repeated one");

            Services.PasswordPolicy.ValidatePassword(req.New);

            var isself = IsSelfUser(userid);

            try
            {
                return await DB.RunInTransactionAsync(db => {
                    var isadmin = Services.AdminHelper.IsAdmin(db, requestuserid);
                    if (!isself && !isadmin)
                        return Forbidden;

                    var user = db.SelectItemById<Database.User>(userid);
                    if (!isadmin && !Ceen.Security.PBKDF2.ComparePassword(req.Current, user.Password))
                        return Status(BadRequest, "The current password is not correct");
                    user.Password = Ceen.Security.PBKDF2.CreatePBKDF2(req.New);
                    db.UpdateItem(user);
                    return OK;
                });
            }
            catch (Exception ex)
            {
                var t = Ceen.Extras.CRUDExceptionHelper.WrapExceptionMessage(ex);
                if (t != null)
                    return t;

                throw;
            }
        }

        [HttpPut]
        [Ceen.Mvc.Name("password")]
        public Task<IResult> Put(UpdatePasswordInfo req)
        {
            // Non-admin users can only update their own password
            return UpdatePasswordAsync(Context.UserID, req);
        }
    }
}