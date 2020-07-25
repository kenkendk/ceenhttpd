using System.Data;
using System.Threading.Tasks;
using Ceen.Mvc;
using Ceen.Database;
using System.Collections.Generic;
using System.Linq;
using System;
using Ceen;
using Ceen.Extras;

namespace Ceen.PaaS.AdminHandlers
{
    /// <summary>
    /// The CRUD helper, but with required admin marker
    /// </summary>
    /// <typeparam name="TKey">The key-type for the records</typeparam>
    /// <typeparam name="TValue">The value-type for the records</typeparam>
    [RequireHandler(typeof(Services.AdminRequiredHandler))]
    public abstract class AdminCRUDHelper<TKey, TValue> : CRUDHelper<TKey, TValue>
        where TValue : new()
    {
        protected AdminCRUDHelper()
            : base()
        {
        }
    }
}