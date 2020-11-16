using System;
using Ceen.Mvc;

namespace Ceen.PaaS.QueueHandlers
{
    /// <summary>
    /// Marker interface for the queue
    /// </summary>
    [Name("queue")]
    public interface IQueue : Ceen.Mvc.IControllerPrefix
    {
    }

    /// <summary>
    /// Marker interface for choosing the queue v1
    /// </summary>
    [Name("v1")]
    public interface IQueueV1: IQueue
    {
        
    }
}
