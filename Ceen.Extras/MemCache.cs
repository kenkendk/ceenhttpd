using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceen.Extras
{
    /// <summary>
    /// Simple cache with events
    /// </summary>
    public class MemCache : IModuleWithSetup
    {
        /// <summary>
        /// The maximum number of items in the cache
        /// </summary>
        public long MaxCacheItems { get; set; } = 10000;

        /// <summary>
        /// The lifetime of items in cache, zero means forever
        /// </summary>
        public TimeSpan DefaultExpirationTime { get; set; } = new TimeSpan(0);

        /// <summary>
        /// The base cache
        /// </summary>
        private static Ceen.LRUCache<KeyValuePair<DateTime, object>> _cache;

        /// <summary>
        /// The update handlers
        /// </summary>
        private static readonly Dictionary<string, List<Func<string, Task>>> _updateHandlers = new Dictionary<string, List<Func<string, Task>>>();

        /// <summary>
        /// The default expiration value
        /// </summary>
        private static TimeSpan _defaultExpiration;

        /// <summary>
        /// The lock guarding the _updateHandlers dictionary
        /// </summary>
        private static readonly Ceen.AsyncLock _lock = new Ceen.AsyncLock();

        /// <summary>
        /// Expiration callback handler method
        /// </summary>
        /// <param name="key">The key of the item being updated or deleted</param>
        /// <param name="item">The item being updated or deleted</param>
        /// <param name="deleting"><c>true</c> if the item is being deleted, <c>false</c> otherwise</param>
        /// <returns>An awaitable task</returns>
        private static Task OnExpire(string key, KeyValuePair<DateTime, object> item, bool deleting)
        {
            if (deleting)
                return NotifyChangeListeners(key, item.Value);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Handler for expiring or updating items
        /// </summary>
        /// <param name="key">The key of the item being added or deleted</param>
        /// <param name="item">The item being replaced (the old value)</param>
        /// <returns>An awaitable task</returns>
        private static Task NotifyChangeListeners(string key, object item)
        {
            if (_updateHandlers.ContainsKey(key))
                Task.Run(async () =>
                {
                    _updateHandlers.TryGetValue(key, out var lst);
                    if (lst == null || lst.Count == 0)
                        return;

                    Func<string, Task>[] items;
                    using (await _lock.LockAsync())
                        items = lst.ToArray();

                    foreach (var n in items)
                        await n(key);
                });

            return Task.FromResult(true);
        }

        /// <summary>
        /// Registers a function to monitor one or more keys
        /// </summary>
        /// <param name="keys">The keys to monitor</param>
        /// <param name="responder">The function to invoke on changes</param>
        /// <returns>An awaitable task</returns>
        public static Task MonitorKeys(IEnumerable<string> keys, Func<string, Task> responder)
        {
            return _lock.LockedAsync(() =>
            {
                foreach (var k in keys)
                {
                    if (!_updateHandlers.TryGetValue(k, out var lst))
                        lst = _updateHandlers[k] = new List<Func<string, Task>>();

                    if (!lst.Contains(responder))
                        lst.Add(responder);
                }
            });
        }

        /// <summary>
        /// Unregisters a function to monitor one or more keys
        /// </summary>
        /// <param name="keys">The keys to unmonitor</param>
        /// <param name="responder">The function to remove</param>
        /// <returns>An awaitable task</returns>
        public static Task UnmonitorKeys(IEnumerable<string> keys, Func<string, Task> responder)
        {
            return _lock.LockedAsync(() =>
            {
                foreach (var k in keys)
                {
                    if (!_updateHandlers.TryGetValue(k, out var lst))
                        continue;

                    lst.Remove(responder);
                    if (lst.Count == 0)
                        _updateHandlers.Remove(k);
                }
            });
        }

        /// <summary>
        /// Registers a function to monitor a key
        /// </summary>
        /// <param name="keys">The key to monitor</param>
        /// <param name="responder">The function to invoke on changes</param>
        /// <returns>An awaitable task</returns>
        public static Task MonitorKey(string key, Func<string, Task> responder)
        {
            return MonitorKeys(new string[] { key }, responder);
        }

        /// <summary>
        /// Unregisters a function to monitor a key
        /// </summary>
        /// <param name="keys">The key to unmonitor</param>
        /// <param name="responder">The function to remove</param>
        /// <returns>An awaitable task</returns>
        public static Task UnmonitorKey(string key, Func<string, Task> responder)
        {
            return UnmonitorKeys(new string[] { key }, responder);
        }

        /// <summary>
        /// Adds an item to the cache
        /// </summary>
        /// <param name="key">The key to store the item under</param>
        /// <param name="item">The item to store</param>
        /// <returns>An awaitable task</returns>
        public static Task AddItem(string key, object item)
        {
            return AddItem(key, item, _defaultExpiration.Ticks == 0 ? new DateTime(0) : DateTime.Now + _defaultExpiration);
        }

        /// <summary>
        /// Adds an item to the cache
        /// </summary>
        /// <param name="key">The key to store the item under</param>
        /// <param name="item">The item to store</param>
        /// <param name="expires">The time the item expires</param>
        /// <returns>An awaitable task</returns>
        public static Task AddItem(string key, object item, TimeSpan expires)
        {
            return AddItem(key, item, DateTime.Now + expires);
        }

        /// <summary>
        /// Adds an item to the cache
        /// </summary>
        /// <param name="key">The key to store the item under</param>
        /// <param name="item">The item to store</param>
        /// <param name="expires">The time the item expires</param>
        /// <returns>An awaitable task</returns>
        public static async Task AddItem(string key, object item, DateTime expires)
        {
            await _cache.AddOrReplaceAsync(key, new KeyValuePair<DateTime, object>(expires, item));
            await NotifyChangeListeners(key, item);
        }

        /// <summary>
        /// Removes an item
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>An awaitable task</returns>
        public static Task RemoveItem(string key)
        {
            return _cache.TryGetUnlessAsync(key, (a, b) => true);
        }

        /// <summary>
        /// Removes an item
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>An awaitable task</returns>
        public static async Task<object> TryGetValue(string key)
        {
            var exp = DateTime.Now;
            var item = await _cache.TryGetUnlessAsync(key, (a, b) => b.Key.Ticks != 0 && b.Key < exp);
            return item.Key ? item.Value.Value : null;
        }

        /// <summary>
        /// Sets up the cache after configuration
        /// </summary>
        public void AfterConfigure()
        {
            if (_cache != null)
                throw new InvalidOperationException("Can only initialize memcache once");

            _defaultExpiration = DefaultExpirationTime;
            _cache = new LRUCache<KeyValuePair<DateTime, object>>(
                countlimit: MaxCacheItems,
                expirationHandler: OnExpire
            );
        }

        /// <summary>
        /// Helper method to provide type-safe access to a cache value
        /// </summary>
        /// <typeparam name="T">The type store in the cache</typeparam>
        public class CacheHelperInstance<T>
            where T : class
        {
            /// <summary>
            /// The key used for the cache
            /// </summary>
            private readonly string m_key;
            /// <summary>
            /// The default expiration time to use
            /// </summary>
            private readonly TimeSpan m_defaultExpiration;

            /// <summary>
            /// Constructs a new CacheHelperInstance
            /// </summary>
            /// <param name="key">The key used to store the data</param>
            public CacheHelperInstance(string key)
                : this(key, MemCache._defaultExpiration)
            {}

            /// <summary>
            /// Constructs a new CacheHelperInstance
            /// </summary>
            /// <param name="key">The key used to store the data</param>
            /// <param name="defaultExpiration">The default expiration time to use</param>
            public CacheHelperInstance(string key, TimeSpan defaultExpiration)
            {
                m_key = key ?? throw new ArgumentNullException(nameof(key));
                m_defaultExpiration = defaultExpiration;
            }

            /// <summary>
            /// Clears the key from the cache
            /// </summary>
            /// <returns>An awaitable task</returns>
            public Task InvalidateAsync() => MemCache.RemoveItem(m_key);
            /// <summary>
            /// Gets the value from the cache, or null
            /// </summary>
            /// <returns>The value or null</returns>
            public async Task<T> TryGetValueAsync() => (await MemCache.TryGetValue(m_key)) as T;
            /// <summary>
            /// Gets the value from the cache, or creates it
            /// </summary>
            /// <param name="p">The method used to create the value</param>
            /// <returns>The value or null</returns>
            public Task<T> TryGetValueAsync(Func<T> p) => TryGetValueAsync(() => Task.FromResult(p()));
            /// <summary>
            /// Gets the value from the cache, or creates it
            /// </summary>
            /// <param name="p">The method used to create the value</param>
            /// <returns>The value or null</returns>
            public async Task<T> TryGetValueAsync(Func<Task<T>> p)
            {
                T res;
                if ((res = await TryGetValue(m_key) as T) == null)
                    await AddItem(m_key, res = await p());

                return res;
            }
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(T value) => MemCache.AddItem(m_key, value, m_defaultExpiration.Ticks == 0 ? new DateTime(0) : DateTime.Now + m_defaultExpiration);
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <param name="expires">The life-time of the entry</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(T value, TimeSpan expires) => MemCache.AddItem(m_key, value, expires);
        }

        /// <summary>
        /// Helper method to provide type-safe access to a cache value
        /// with subkeys
        /// </summary>
        /// <typeparam name="T">The type store in the cache</typeparam>
        public class CacheHelperInstanceWithSubkey<T>
            where T : class
        {
            /// <summary>
            /// The key used for the cache
            /// </summary>
            private readonly string m_key;
            /// <summary>
            /// The default expiration time to use
            /// </summary>
            private readonly TimeSpan m_defaultExpiration;

            /// <summary>
            /// Constructs a new CacheHelperInstanceWithSubkey
            /// </summary>
            /// <param name="key">The key used to store the data</param>
            public CacheHelperInstanceWithSubkey(string key)
                : this(key, MemCache._defaultExpiration)
            { }

            /// <summary>
            /// Constructs a new CacheHelperInstanceWithSubkey
            /// </summary>
            /// <param name="key">The key used to store the data</param>
            /// <param name="defaultExpiration">The default expiration time to use</param>
            public CacheHelperInstanceWithSubkey(string key, TimeSpan defaultExpiration)
            {
                m_key = key ?? throw new ArgumentNullException(nameof(key));
                m_defaultExpiration = defaultExpiration;
            }

            /// <summary>
            /// Method that creates a full key, given the sub-key
            /// </summary>
            /// <param name="sub">The sub key</param>
            /// <returns>The full key</returns>
            protected virtual string GetKeyForSubkey(string sub) => m_key + ":" + sub;

            /// <summary>
            /// Clears a subkey from the cache
            /// </summary>
            /// <returns>An awaitable task</returns>
            public Task InvalidateAsync(string subkey) => MemCache.RemoveItem(GetKeyForSubkey(subkey));
            /// <summary>
            /// Gets the value from the cache, or null
            /// </summary>
            /// <returns>The value or null</returns>
            public async Task<T> TryGetValueAsync(string subkey) => (await MemCache.TryGetValue(GetKeyForSubkey(subkey))) as T;
            /// <summary>
            /// Gets the value from the cache, or creates it
            /// </summary>
            /// <param name="p">The method used to create the value</param>
            /// <returns>The value or null</returns>
            public Task<T> TryGetValueAsync(string subkey, Func<T> p) => TryGetValueAsync(subkey, () => Task.FromResult(p()));
            /// <summary>
            /// Gets the value from the cache, or creates it
            /// </summary>
            /// <param name="p">The method used to create the value</param>
            /// <returns>The value or null</returns>
            public async Task<T> TryGetValueAsync(string subkey, Func<Task<T>> p)
            {
                T res;
                if ((res = await TryGetValue(GetKeyForSubkey(subkey)) as T) == null)
                    await AddItem(GetKeyForSubkey(subkey), res = await p());

                return res;
            }
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(string subkey, T value) => MemCache.AddItem(GetKeyForSubkey(subkey), value, m_defaultExpiration.Ticks == 0 ? new DateTime(0) : DateTime.Now + m_defaultExpiration);
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <param name="expires">The life-time of the entry</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(string subkey, T value, TimeSpan expires) => MemCache.AddItem(GetKeyForSubkey(subkey), value, expires);
        }
    }
}