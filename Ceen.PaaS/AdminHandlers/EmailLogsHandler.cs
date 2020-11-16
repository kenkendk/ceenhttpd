using System.Threading.Tasks;
using Ceen;

namespace Ceen.PaaS.AdminHandlers
{
    public class EmailLogsHandler: AdminCRUDHelper<long, Database.SentEmailLog>, IAdminAPIv1
    {
        protected override Ceen.Extras.DatabaseBackedModule Connection => DatabaseInstance.GetInstance();
        protected override AccessType[] AllowedAccess => ReadOnlyAccess;
    }
}