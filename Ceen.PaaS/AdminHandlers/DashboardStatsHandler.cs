using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Ceen;
using Ceen.Mvc;
using Ceen.Database;
using System.Linq;

namespace Ceen.PaaS.AdminHandlers
{
    /// <summary>
    /// Handler for the dashboard stats
    /// </summary>
    [RequireHandler(typeof(Ceen.PaaS.Services.AdminRequiredHandler))]
    public class DashboardStatsHandler : API.ControllerBase, IAdminAPIv1
    {
        /// <summary>
        /// The UNIX Epoch value
        /// </summary>
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The log module instance
        /// </summary>
        private Ceen.Extras.LogModule _logModule;
        /// <summary>
        /// Lazy-loaded cached reference to the log module
        /// </summary>
        protected Ceen.Extras.LogModule HttpLogModule
        {
            get
            {
                if (_logModule == null)
                    _logModule = Ceen.Context.Current.GetItemsOfType<Ceen.Extras.LogModule>().First();

                return _logModule;
            }
        }

        [HttpPost]
        public virtual async Task<IResult> Index()
        {
            return Json(await IndexResultsAsync(DateTime.Now.AddDays(-7)));
        }

        /// <summary>
        /// Overrideable result generator for the index handler, providing the dashboard overview results
        /// </summary>
        /// <param name="timelimit">The time to display events for in the dashboard</param>
        /// <returns>An an object prepared for JSON handling</returns>
        protected virtual async Task<Dictionary<string, object>> IndexResultsAsync(DateTime timelimit)
        {
            var httpstats = await HttpLogModule?.RunInTransactionAsync(db => new {
                OK = db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > timelimit && x.ResponseStatusCode <= 399),
                ClientError = db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > timelimit && x.ResponseStatusCode >= 400 && x.ResponseStatusCode <= 499),
                ServerError = db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > timelimit && x.ResponseStatusCode >= 500),
                LandingPage = db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > timelimit && x.RequestUrl == "/"),
            });

            return await DB.RunInTransactionAsync(db => 
                new Dictionary<string, object>() {
                    { 
                        "Signup", new {
                            WaitListSize = db.SelectCount<Database.Signup.SignupEntry>(x => x.Status == Database.Signup.SignupStatus.Confirmed),
                            ActivatedUsers = db.SelectCount<Database.Signup.SignupEntry>(x => x.Status == Database.Signup.SignupStatus.Activated),
                            NewConfirmedSignups = db.SelectCount<Database.Signup.SignupEntry>(x => x.LastAttempt > timelimit && x.Status == Database.Signup.SignupStatus.Confirmed),
                            NonConfirmedSignups = db.SelectCount<Database.Signup.SignupEntry>(x => x.LastAttempt > timelimit && (x.Status == Database.Signup.SignupStatus.Created || x.Status == Database.Signup.SignupStatus.Failed)),
                        }
                    },
                    {
                        "Email", new {
                            EmailsSent = db.SelectCount<Database.SentEmailLog>(x => x.When > timelimit),
                        }
                    },
                    {
                        "Http", httpstats
                    }
                }
            );
        }

        /// <summary>
        /// The request for graph data
        /// </summary>
        public class GraphQuery
        {
            public int From;
            public int To;
            public int Buckets;
            public string Type;
        }

        /// <summary>
        /// Time range for graph queries
        /// </summary>
        public struct TimeRange
        {
            public DateTime From;
            public DateTime To;
        }

        /// <summary>
        /// Builds a graph for a particular feature
        /// </summary>
        /// <param name="start">The unix epoch based start of the range</param>
        /// <param name="end">The unix epoch based end of the range</param>
        /// <param name="type">The query type</param>
        /// <returns></returns>
        [HttpPost]
        public virtual async Task<IResult> Graph(GraphQuery req)            
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Type))
                return BadRequest;

            if (req.To - req.From < 100)
                return Status(BadRequest, "End must be larger than start by at least 100 seconds");
            if (req.Buckets < 3 || req.Buckets > 100)
                return Status(BadRequest, "The buckets must be [3-365]");

            var from = UnixEpoch.AddSeconds(req.From);
            var to = UnixEpoch.AddSeconds(req.To);
            var bucketsize = TimeSpan.FromSeconds((req.To - req.From) / req.Buckets);

            // Compute to and from ranges
            var fromtimes = new DateTime[req.Buckets];
            var totimes = new DateTime[req.Buckets];
            var prev = from;
            for(var i = 0; i < req.Buckets; i++)
                prev = totimes[i] = (fromtimes[i] = prev) + bucketsize;

            // Make sure we cover the full range
            totimes[req.Buckets - 1] = to;

            // Make it as an array
            var dt = fromtimes.Zip(totimes, (x, y) => new TimeRange { From = x, To = y }).ToArray();

            // Compute the response in an overideable manner
            return await CreateGraphResponse(req, dt);

        }

        /// <summary>
        /// Creates the actual response from the request and a computed set of intervals
        /// </summary>
        /// <param name="req">The request to service</param>
        /// <param name="ranges">The computed range buckets</param>
        /// <returns>A JSON result</returns>
        protected virtual async Task<IResult> CreateGraphResponse(GraphQuery req, TimeRange[] ranges)
        {
            switch (req.Type.ToLowerInvariant())
            {
                case "signup":
                    return Json(await DB.RunInTransactionAsync(db => 
                        new {
                            WaitListSize = 
                                ranges.Select(t =>
                                    db.SelectCount<Database.Signup.SignupEntry>(x => x.Status == Database.Signup.SignupStatus.Confirmed && x.When > t.From && x.When <= t.To)
                                ).ToArray(),
                            ActivatedUsers = 
                                ranges.Select(t =>
                                    db.SelectCount<Database.Signup.SignupEntry>(x => x.Status == Database.Signup.SignupStatus.Activated && x.WhenActivated > t.From && x.WhenActivated <= t.To)
                                ).ToArray(),
                            ConfirmedSignups = 
                                ranges.Select(t =>
                                    db.SelectCount<Database.Signup.SignupEntry>(x => x.LastAttempt > t.From && x.LastAttempt <= t.To && x.Status == Database.Signup.SignupStatus.Confirmed)
                                ).ToArray(),
                            NonConfirmedSignups = 
                                ranges.Select(t => 
                                    db.SelectCount<Database.Signup.SignupEntry>(x => x.LastAttempt > t.From && x.LastAttempt <= t.To && (x.Status == Database.Signup.SignupStatus.Created || x.Status == Database.Signup.SignupStatus.Failed))
                                ).ToArray()
                        }));

                case "email":
                    return Json(await DB.RunInTransactionAsync(db =>
                        new {
                            Sent = 
                                ranges.Select(t =>
                                    db.SelectCount<Database.SentEmailLog>(x => x.When > t.From && x.When <= t.To)
                                ).ToArray()
                        }
                    ));

                case "http":
                    return Json(await HttpLogModule.RunInTransactionAsync(db =>
                        new
                        {
                            OK =
                                ranges.Select(t =>
                                    db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > t.From && x.Started <= t.To && x.ResponseStatusCode <= 399)
                                ).ToArray(),
                            ClientError =
                                ranges.Select(t =>
                                    db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > t.From && x.Started <= t.To && x.ResponseStatusCode > 399 && x.ResponseStatusCode <= 499)
                                ).ToArray(),
                            ServerError =
                                ranges.Select(t =>
                                    db.SelectCount<Ceen.Extras.LogModule.HttpLogEntry>(x => x.Started > t.From && x.Started <= t.To && x.ResponseStatusCode >= 500)
                                ).ToArray()
                        }
                    ));

                default:
                    return Status(BadRequest, "No such query type");
            }
        }

    }
}
