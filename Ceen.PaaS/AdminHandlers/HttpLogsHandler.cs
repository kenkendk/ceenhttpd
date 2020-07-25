using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using System.Linq;

namespace Ceen.PaaS.AdminHandlers
{
    public class HttpLogsHandler : AdminCRUDHelper<string, Ceen.Extras.LogModule.HttpLogEntry>, IAdminAPIv1
    {
        /// <summary>
        /// The log module instance
        /// </summary>
        private Ceen.Extras.LogModule _logmodule;

        /// <summary>
        /// Lazy-loaded cached reference to the log module
        /// </summary>
        private Ceen.Extras.LogModule LogModule
        {
            get
            {
                if (_logmodule == null)
                    _logmodule = Ceen.Context.Current.GetItemsOfType<Ceen.Extras.LogModule>().FirstOrDefault();
                return _logmodule;
            }
        }
        protected override Ceen.Extras.DatabaseBackedModule Connection => LogModule;
        protected override AccessType[] AllowedAccess => ReadOnlyAccess;

        [HttpGet]
        [Route("{id}/lines")]
        public async Task<IResult> LogLines(string id)
        {
            return Json(
                await Connection.RunInTransactionAsync(db => 
                    db
                        .Select<Ceen.Extras.LogModule.HttpLogEntryLine>(x => x.ParentID == id)
                        .ToArray()
                )
            );
        }
    }
}