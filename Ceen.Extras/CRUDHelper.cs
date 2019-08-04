using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ceen.Database;
using Ceen.Mvc;

namespace Ceen.Extras
{
    /// <summary>
    /// The parameters to a list request
    /// </summary>
    public class ListRequest
    {
        /// <summary>
        /// The start of the list results
        /// </summary>
        public int Offset;
        /// <summary>
        /// The maximum number of list results
        /// </summary>
        public int Count;
        /// <summary>
        /// The filter to apply
        /// </summary>
        public string Filter;
        /// <summary>
        /// The sort order to use
        /// </summary>
        public string SortOrder;
    }

    /// <summary>
    /// The response to a list request
    /// </summary>
    public class ListResponse
    {
        /// <summary>
        /// The offset of these results
        /// </summary>
        public long Offset;
        /// <summary>
        /// The total number of entries
        /// </summary>
        public long Total;
        /// <summary>
        /// The result data
        /// </summary>
        public Array Result;

        /// <summary>
        /// Creates a new list response
        /// </summary>
        public ListResponse()
        {
        }

        /// <summary>
        /// Creates a new list response and uses the array to fill the total count
        /// </summary>
        /// <param name="data">The data to send</param>
        public ListResponse(Array data)
        {
            Result = data;
            if (data != null)
                Total = data.Length;
        }

        /// <summary>
        /// Creates a new list response with manual specification of the fields
        /// </summary>
        /// <param name="data">The data to send</param>
        /// <param name="offset">The current offset</param>
        /// <param name="total">The total number of lines</param>
        public ListResponse(Array data, long offset, long total)
        {
            Result = data;
            Offset = offset;
            Total = total;
        }
    }

    /// <summary>
    /// Simple CRUD helper
    /// </summary>
    /// <typeparam name="TKey">The key type</typeparam>
    /// <typeparam name="TValue">The item value type</typeparam>
    public abstract class CRUDHelper<TKey, TValue> :  Controller
        where TValue : new()
    {
        /// <summary>
        /// The database instance to use
        /// </summary>
        protected abstract Ceen.Extras.DatabaseBackedModule Connection { get; }

        /// <summary>
        /// Helper property for full access
        /// </summary>
        protected static readonly AccessType[] FullAccess = new AccessType[] { AccessType.Add, AccessType.Delete, AccessType.Get, AccessType.List, AccessType.Update };

        /// <summary>
        /// Helper property for read-only access
        /// </summary>
        protected static readonly AccessType[] ReadOnlyAccess = new AccessType[] { AccessType.Get, AccessType.List };

        /// <summary>
        /// The access type allowed
        /// </summary>
        protected abstract AccessType[] AllowedAccess { get; }

        /// <summary>
        /// Helper to grant access to the operation
        /// </summary>
        /// <param name="type">The operation being attempted</param>
        /// <param name="id">The key of the item, unless adding or listing</param>
        /// <returns></returns>
        public virtual Task Authorize(AccessType type, TKey id)
        {
            if (AllowedAccess.Contains(type))
                return Task.FromResult(true);

            throw new HttpException(HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// Hook function for patching the query before submitting it
        /// </summary>
        /// <param name="type">The operation being attempted</param>
        /// <param name="id">The key of the item, unless adding or listing</param>
        /// <param name="q">The query</param>
        /// <returns>The query</returns>
        public virtual Query<TValue> OnQuery(AccessType type, TKey id, Query<TValue> q) => q;

        /// <summary>
        /// Avoid repeated allocations of unused tasks
        /// </summary>
        /// <returns></returns>
        protected readonly Task m_completedTask = Task.FromResult(true);

        /// <summary>
        /// Hook function for validating an item before inserting it
        /// </summary>
        /// <param name="item">The item being inserted</param>
        protected virtual Task BeforeInsertAsync(TValue item) => m_completedTask;

        /// <summary>
        /// Hook function for validating an item before inserting it
        /// </summary>
        /// <param name="key">The ID of the item being inserted</param>
        /// <param name="item">The item being inserted</param>
        protected virtual Task BeforeUpdateAsync(TKey key, Dictionary<string, object> values) => m_completedTask;

        /// <summary>
        /// The different access types
        /// </summary>
        public enum AccessType
        {
            /// <summary>Read an item</summary>
            Get,
            /// <summary>Update an item</summary>
            Update,
            /// <summary>Add an item</summary>
            Add,
            /// <summary>List items</summary>
            List,
            /// <summary>Delete an item</summary>
            Delete
        }

        /// <summary>
        /// Statically allocated instance of a null result
        /// </summary>
        protected static readonly Task<IResult> NULL_RESULT_TASK = Task.FromResult<IResult>(null);

        /// <summary>
        /// Custom exception handler, used to report messages from validation to the user
        /// </summary>
        /// <param name="ex">The error to handle</param>
        /// <returns>A result if the error was handled, <c>null</c> otherwise</returns>
        protected virtual Task<IResult> HandleExceptionAsync(Exception ex)
        {
            if (ex is ValidationException vex)
            {
                var pre = string.Empty;
                if (vex.Member != null)
                    pre = vex.Member.Name + " - ";
                return Task.FromResult(Status(BadRequest, pre + vex.Message));
            }

            return NULL_RESULT_TASK;
        }

        /// <summary>
        /// Gets an item
        /// </summary>
        /// <param name="id">The item to get</param>
        /// <returns>The response</returns>
        [HttpGet]
        [Ceen.Mvc.Name("index")]
        [Route("{id}")]
        public virtual async Task<IResult> Get(TKey id)
        {
            await Authorize(AccessType.Get, id);
            try
            {
                var q = Connection
                        .Query<TValue>()
                        .Select()
                        .MatchPrimaryKeys(new object[] { id })
                        .Limit(1);

                OnQuery(AccessType.Get, id, q);

                var res = await Connection.RunInTransactionAsync(db => db.SelectSingle(q));
                if (res == null)
                    return NotFound;

                return Json(res);
            }
            catch (Exception ex)
            {
                var r = await HandleExceptionAsync(ex);
                if (r != null)
                    return r;
                throw;
            }
        }

        /// <summary>
        /// Updates an item
        /// </summary>
        /// <param name="id">The item ID</param>
        /// <param name="values">The values to patch with</param>
        /// <returns>The response</returns>
        [HttpPut]
        [HttpPatch]
        [Ceen.Mvc.Name("index")]
        [Route("{id}")]
        public virtual async Task<IResult> Patch(TKey id, Dictionary<string, object> values)
        {
            await Authorize(AccessType.Update, id);
            if (values == null || values.Count == 0)
                return BadRequest;

            try
            {
                await BeforeUpdateAsync(id, values);

                // We need to keep the lock, otherwise we could have 
                // a race between reading the current item and updating

                // We do not want to deserialize the data while holding the lock,
                // as that would make a any hanging transfer hold the lock.
                // So we accept a dictionary as input, but we cannot call `Populate`
                // with the dictionary directly, so we re-serialize it to a string,
                // such that we can call `Populate` without any potential hang.

                // This is not ideal, but we cannot accept a TValue as input,
                // because it might be partial (PATCH) and applying the partial
                // input would cause data loss, and we cannot go back and see which
                // properties were present

                // Re-build a string representation of the data
                var jsonSr = Newtonsoft.Json.JsonConvert.SerializeObject(values);

                return await Connection.RunInTransactionAsync(db =>
                {
                    var item = db.SelectItemById<TValue>(id);
                    if (item == null)
                        return NotFound;

                    // Patch with the source data
                    Newtonsoft.Json.JsonConvert.PopulateObject(jsonSr, item);

                    var q = Connection
                        .Query<TValue>()
                        .Update(item)
                        .MatchPrimaryKeys(new object[] { id })
                        .Limit(1);

                    OnQuery(AccessType.Update, id, q);

                    if (db.Update(q) > 0)
                        return Json(db.SelectItemById<TValue>(id));
                    return NotFound;
                });
            }
            catch (Exception ex)
            {
                var r = await HandleExceptionAsync(ex);
                if (r != null)
                    return r;
                throw;
            }
        }

        /// <summary>
        /// Inserts an item into the database
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The response</returns>
        [HttpPost]
        [Ceen.Mvc.Name("index")]
        public virtual async Task<IResult> Post(TValue item)
        {
            await Authorize(AccessType.Add, default(TKey));
            if (item == null)
                return BadRequest;

            try
            {
                await BeforeInsertAsync(item);

                var q = Connection
                    .Query<TValue>()
                    .Insert(item);

                OnQuery(AccessType.Add, default(TKey), q);

                return Json(await Connection.RunInTransactionAsync(db =>
                {
                    db.Insert(q);
                    return item;
                }));
            }
            catch (Exception ex)
            {
                var r = await HandleExceptionAsync(ex);
                if (r != null)
                    return r;
                throw;
            }
        }

        /// <summary>
        /// Deletes an item from the database
        /// </summary>
        /// <param name="id">The key of the item to delete</param>
        /// <returns>The response</returns>
        [HttpDelete]
        [Ceen.Mvc.Name("index")]
        [Route("{id}")]
        public virtual async Task<IResult> Delete(TKey id)
        {
            await Authorize(AccessType.Delete, id);

            try
            {
                var q = Connection
                        .Query<TValue>()
                        .Delete()
                        .Limit(1)
                        .MatchPrimaryKeys(new object[] { id });

                OnQuery(AccessType.Delete, id, q);

                return await Connection.RunInTransactionAsync(db =>
                {
                    if (db.Delete(q) > 0)
                        return OK;
                    return NotFound;
                });
            }
            catch (Exception ex)
            {
                var r = await HandleExceptionAsync(ex);
                if (r != null)
                    return r;
                throw;
            }
        }

        /// <summary>
        /// Gets the current list of items
        /// </summary>
        /// <returns>The results</returns>
        [HttpGet]
        [Ceen.Mvc.Name("index")]
        public virtual Task<IResult> List(ListRequest request)
        {
            return Search(request);
        }

        /// <summary>
        /// Gets the current list of items
        /// </summary>
        /// <returns>The results</returns>
        [HttpPost]
        [Ceen.Mvc.Route("search")]
        public virtual async Task<IResult> Search(ListRequest request)
        {
            await Authorize(AccessType.List, default(TKey));
            if (request == null)
                return BadRequest;

            try
            {
                var q = Connection
                    .Query<TValue>()
                    .Select()
                    .Where(request.Filter)
                    .OrderBy(request.SortOrder)
                    .Offset(request.Offset)
                    .Limit(request.Count);

                OnQuery(AccessType.List, default(TKey), q);

                return Json(await Connection.RunInTransactionAsync(db =>
                    new ListResponse()
                    {
                        Offset = request.Offset,
                        Total = db.SelectCount(q),
                        Result = db.Select(q).ToArray()
                    }
                ));
            }
            catch (Exception ex)
            {
                var r = await HandleExceptionAsync(ex);
                if (r != null)
                    return r;
                throw;
            }
        }
    }
}
