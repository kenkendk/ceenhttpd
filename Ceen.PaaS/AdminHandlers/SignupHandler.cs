using System.Threading.Tasks;
using Ceen.Mvc;
using Ceen.Database;
using System;
using System.Linq;
using System.Collections.Generic;
using Ceen.Extras;

namespace Ceen.PaaS.AdminHandlers
{
    public class SignupHandler : API.ControllerBase, IAdminAPIv1
    {
        [HttpGet]
        [Ceen.Mvc.Name("index")]
        [Route("{id}")]
        public async Task<IResult> Get(long id)
        {
            if (id < 0)
                return BadRequest;

            var res = await DB.RunInTransactionAsync(db => db.SelectItemById<Database.Signup.SignupEntry>(id));
            if (res == null)
                return NotFound;

            return Json(res);
        }

        [HttpPut]
        [Ceen.Mvc.Name("index")]
        [Route("{id}")]
        public async Task<IResult> Put(long id, Dictionary<string, object> values) 
        {
            if (values == null || id < 0)
                return BadRequest;

            // Make the field names case insensitive
            var realvalues = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase);
            await DB.RunInTransactionAsync(db => db.Update<Database.Signup.SignupEntry>(realvalues, x => x.ID == id));
            return OK;
        }
    }

    public class SignupsHandler : API.ControllerBase, IAdminAPIv1
    {
        /// <summary>
        /// Gets the current list of signups
        /// </summary>
        /// <returns>The status code</returns>
        [HttpPost]
        public async Task<IResult> Index(ListRequest request)
        {
            if (request == null)
                return BadRequest;

            var query = DB.Query<Database.Signup.SignupEntry>()
                .Select()
                .Where(request.Filter)
                .OrderBy(request.SortOrder)
                .Offset(request.Offset)
                .Limit(request.Count);

            return Json(await DB.RunInTransactionAsync(db =>
                new ListResponse() {
                    Offset = request.Offset,
                    Total = db.SelectCount(query),
                    Result = db.Select(query).ToArray()
                }
            ));
        }

        /// <summary>
        /// Activates the users
        /// </summary>
        /// <param name="users">The users to activate</param>
        /// <returns>The status code</returns>
        [HttpPost]
        public async Task<IResult> Activate(Database.User[] users) 
        {
            foreach (var u in users)
                await Services.Activation.ActivateUser(u.ID);

            return OK;
        }
    }
}