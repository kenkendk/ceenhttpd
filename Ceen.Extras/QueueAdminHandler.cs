using System;
using System.Linq;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using System.Threading.Tasks;
using System.Collections.Generic;
using Ceen.Extras;

namespace Ceen.Extras
{
    /// <summary>
    /// Helper for displaying a the loaded queues
    /// </summary>
    public class QueueDisplay
    {
        public string Name;
        public string Description;
        public string MaxRate;
        public string Backoff;
        public int Concurrent;
        public int Retries;
        public int Rate;
        public int Running;
        public bool Active;
        public string CrashMsg;
        public long QueueSize;
    }

    [Route("queues")]
    public abstract class QueuesHandler : Controller
    {
        /// <summary>
        /// Gets all currently running queues
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IResult> Index()
        {
            var res = new List<QueueDisplay>();

            foreach (var x in Ceen.Context.Current.GetItemsOfType<Ceen.Extras.QueueModule>())
                res.Add(new QueueDisplay()
                {
                    Name = x.Name,
                    Description = x.Description,
                    MaxRate = x.Ratelimit,
                    Backoff = x.RetryBackoff,
                    Concurrent = x.ConcurrentRequests,
                    Retries = x.MaxRetries,
                    Rate = x.CurrentRate,
                    Running = x.CurrentRunning,
                    Active = x.RunnerActive,
                    CrashMsg = x.RunnerCrashMessage,
                    QueueSize = await x.GetQueueSizeAsync()
                });

            return Json(new ListResponse() { Offset = 0, Total = res.Count, Result = res.ToArray() });
        }
    }

    /// <summary>
    /// Handles queries to a specific queue
    /// </summary>
    [Route("queue/{queue}")]
    public abstract class QueueHandler : CRUDHelper<string, Ceen.Extras.QueueModule.QueueEntry>
    {
        private static Dictionary<string, Ceen.Extras.QueueModule> _queues;
        private static Ceen.Extras.QueueModule GetQueue()
        {
            Context.Request.RequestState.TryGetValue("queue", out var n);
            var qn = n as string;
            if (string.IsNullOrWhiteSpace(qn))
                throw new Exception("Unexpected empty queue parameter");

            return GetQueue(qn);
        }

        private static Ceen.Extras.QueueModule GetQueue(string name)
        {
            if (_queues == null)
                _queues = Ceen.Context.Current.GetItemsOfType<Ceen.Extras.QueueModule>().ToDictionary(x => x.Name);

            _queues.TryGetValue(name, out var q);
            if (q == null)
                throw new HttpException(HttpStatusCode.NotFound);

            return q;
        }

        protected override Ceen.Extras.DatabaseBackedModule Connection => GetQueue();
        protected override AccessType[] AllowedAccess => FullAccess;

        public override Query<Ceen.Extras.QueueModule.QueueEntry> OnQuery(AccessType type, string id, Query<Ceen.Extras.QueueModule.QueueEntry> q)
        {
            var queue = GetQueue();

            if (q.Parsed.Type == QueryType.Insert)
                ((Ceen.Extras.QueueModule.QueueEntry)q.Parsed.InsertItem).QueueName = queue.Name;
            else if (q.Parsed.Type == QueryType.Update)
                q.Parsed.UpdateValues.Remove(nameof(Ceen.Extras.QueueModule.QueueEntry.QueueName));
            else
                q = q.Where(x => x.QueueName == queue.Name);

            return q;
        }

        [HttpPost]
        [Route("{key}/run")]
        public IResult Run(string queue, long key)
        {
            var q = Ceen.Extras.QueueModule.GetQueue(queue);
            if (q == null)
                return NotFound;

            q.ForceRun(key);

            return OK;
        }

        [HttpPost]
        [Route("{key}/lines")]
        public async Task<IResult> Lines(string queue, long key)
        {
            var q = Ceen.Extras.QueueModule.GetQueue(queue);
            if (q == null)
                return NotFound;

            return Json(await q.RunInTransactionAsync(db =>
                new ListResponse(db.Select(
                    db.Query<Ceen.Extras.QueueModule.QueueRunLog>()
                    .Select()
                    .Where(x => x.TaskID == key)
                ).ToArray())
            ));

        }

    }

}