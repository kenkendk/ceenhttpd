using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ceen.Database;
using Ceen.Mvc;

namespace Ceen.PaaS.AdminHandlers
{
    /// <summary>
    /// Handler for users, replicated from the normal API space
    /// We need the normal API space, as a non-admin user can update
    /// their own settings, but admin users have more powers
    /// </summary>
    [RequireHandler(typeof(Services.AdminRequiredHandler))]
    public class UserHandler : Ceen.PaaS.API.UserHandler, IAdminAPIv1
    {
        /// <summary>
        /// Helper class for serializing output with extra members
        /// </summary>
        private class ListUserEntry : Database.User
        {
            /// <summary>
            /// Clone constructor
            /// </summary>
            /// <param name="parent">The instance to clone</param>
            public ListUserEntry(Database.User parent)
            {
                foreach(var f in typeof(Database.User).GetFields())
                    f.SetValue(this, f.GetValue(parent));
                this.Password = null;
            }

            /// <summary>
            /// The list of groups the user is a member of, key is ID, value is name
            /// </summary>
            public Dictionary<string, string> Groups;
            /// <summary>
            /// A flag indicating if the user is an admin
            /// </summary>
            public bool Admin;
        }

        /// <summary>
        /// Helper class for creating users
        /// </summary>
        public class CreateUserSettings : OwnUserSettings
        {
            /// <summary>
            /// The password to set
            /// </summary>
            public string Password;
            /// <summary>
            /// The repeat password to set
            /// </summary>
            public string RepeatPassword;
        }

        [HttpPatch]
        [Route("{userid}")]
        [Ceen.Mvc.Name("index")]
        public Task<IResult> Patch(string userid, OwnUserSettings settings)
        {
            // Admin users can update all users
            return UpdateUserAsync(userid, settings);
        }

        [HttpPut]
        [Route("{userid}/password")]
        [Ceen.Mvc.Name("index")]
        public Task<IResult> Put(string userid, UpdatePasswordInfo req)
        {
            // Admin users can change password for all users
            return UpdatePasswordAsync(userid, req);
        }


        [HttpPut]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> Put(CreateUserSettings req)
        {
            if (req == null)
                return BadRequest;

            // This handler has an admin checker required, so we
            // can skip checking for admin status

            try
            {
                var item = new Database.User();
                var userfields = typeof(Database.User).GetFields().ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
                foreach(var f in typeof(CreateUserSettings).GetFields())
                {
                    if (new[] {
                            nameof(CreateUserSettings.ID), 
                            nameof(CreateUserSettings.Password)
                    }.Contains(f.Name))
                        continue;

                    if (userfields.TryGetValue(f.Name, out var field))
                    {
                        if (field.FieldType == f.FieldType)
                            field.SetValue(item, f.GetValue(req));
                        else if (field.FieldType == typeof(bool) && f.FieldType == typeof(Nullable<bool>))
                        {
                            var v = f.GetValue(req) as Nullable<bool>;
                            if (v != null)
                                field.SetValue(item, v.Value);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.RepeatPassword))
                    return Status(BadRequest, "One or more fields are missing");

                if (!string.Equals(req.Password, req.RepeatPassword))
                    return Status(BadRequest, "Passwords do not match");

                Services.PasswordPolicy.ValidatePassword(req.Password);
                item.Password = Ceen.Security.PBKDF2.CreatePBKDF2(req.Password);

                return Json(await DB.RunInTransactionAsync(db => {
                    var res = db.InsertItem(item);
                    if (req.Admin.HasValue && req.Admin.Value)
                        db.InsertItem(new Database.UserGroupIndex() {
                            UserID = res.ID,
                            GroupID = IDConstants.AdminGroupID
                        });
                    return res;
                }));

            }
            catch (Exception ex)
            {
                var t = Ceen.Extras.CRUDExceptionHelper.WrapExceptionMessage(ex);
                if (t != null)
                    return t;

                throw;
            }
        }

        [HttpDelete]
        [Route("{userid}")]
        [Ceen.Mvc.Name("index")]
        public async Task<IResult> Delete(string userid)
        {
            if (string.IsNullOrWhiteSpace(Context.UserID))
                return Forbidden;
            if (string.IsNullOrWhiteSpace(userid))
                return BadRequest;
            if (IsSelfUser(userid))
                return Status(HttpStatusCode.NotAcceptable, "Cannot delete self");

            return await DB.RunInTransactionAsync(db => {
                // This handler has admin required, so we do not need to check adming status
                if (db.DeleteItemById<Database.User>(userid))
                {
                    db.Delete<Database.UserGroupIndex>(x => x.UserID == userid);
                    return OK;
                }
                else
                    return NotFound;
            });    
        }

        /// <summary>
        /// Gets the current list of items
        /// </summary>
        /// <returns>The results</returns>
        [HttpPost]
        [Ceen.Mvc.Route("search")]
        public virtual async Task<IResult> Search(Ceen.Extras.ListRequest request)
        {
            if (request == null)
                return BadRequest;

            // This service has admin-required, so we do not check
            // for admin status here

            try
            {
                var q = DB
                    .Query<Database.User>()
                    .Select()
                    .Where(request.Filter)
                    .OrderBy(request.SortOrder)
                    .Offset(request.Offset)
                    .Limit(request.Count);

                return Json(await DB.RunInTransactionAsync(db =>
                    new Ceen.Extras.ListResponse()
                    {
                        Offset = request.Offset,
                        Total = db.SelectCount(q),
                        Result = db.Select(q).Select(x => {
                            // TODO: this makes a query pr. user, could be batched for all users
                            var groups = db.Select(
                                db.Query<Database.UserGroup>()
                                    .Select()
                                    .WhereIn(nameof(Database.UserGroup.ID), 
                                        db.Query<Database.UserGroupIndex>()
                                            .Select(nameof(Database.UserGroupIndex.GroupID))
                                            .Where(y => y.UserID == x.ID)
                                    )
                            ).ToDictionary(y => y.ID, y => y.Name);

                            return new ListUserEntry(x) {
                                Groups = groups,
                                Admin = groups.ContainsKey(IDConstants.AdminGroupID)
                            };
                        }).ToArray()
                    }
                ));
            }
            catch (Exception ex)
            {
                var t = Ceen.Extras.CRUDExceptionHelper.WrapExceptionMessage(ex);
                if (t != null)
                    return t;

                throw;
            }
        }                        
    }
}