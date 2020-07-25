using System;
using Ceen.Mvc;

namespace Ceen.PaaS.API
{
    public abstract class ControllerBase : Controller
    {
        /// <summary>
        /// The database instance
        /// </summary>
        protected readonly DatabaseInstance DB = DatabaseInstance.GetInstance();
    }
}
