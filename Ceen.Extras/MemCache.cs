using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ceen.Extras
{
    /// <summary>
    /// Simple cache with events
    /// </summary>
    public class MemCache : IModuleWithSetup, INamedModule
    {
        /// <summary>
        /// The named memcache instances
        /// </summary>
        private static readonly Dictionary<string, MemCache> _instances = new Dictionary<string, MemCache>();

        /// <summary>
        /// The maximum number of items in the cache
        /// </summary>
        public long MaxCacheItems { get; set; } = 10000;

        /// <summary>
        /// The lifetime of items in cache, zero means forever
        /// </summary>
        public TimeSpan DefaultExpirationTime { get; set; } = new TimeSpan(0);

        /// <summary>
        /// The module name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The base cache
        /// </summary>
        private Ceen.LRUCache<KeyValuePair<DateTime, object>> m_cache;

        /// <summary>
        /// The update handlers
        /// </summary>
        private readonly Dictionary<string, List<Func<string, Task>>> m_updateHandlers = new Dictionary<string, List<Func<string, Task>>>();

        /// <summary>
        /// The lock guarding the _updateHandlers dictionary
        /// </summary>
        private readonly Ceen.AsyncLock m_lock = new Ceen.AsyncLock();

        /// <summary>
        /// Creates a new anonymous memcache instance
        /// </summary>
        public MemCache() { }
        
        /// <summary>
        /// Creates a new named memcache instance
        /// </summary>
        /// <param name="name">The name of the instance</param>
        public MemCache(string name) { Name = name; }

        /// <summary>
        /// Expiration callback handler method
        /// </summary>
        /// <param name="key">The key of the item being updated or deleted</param>
        /// <param name="item">The item being updated or deleted</param>
        /// <param name="deleting"><c>true</c> if the item is being deleted, <c>false</c> otherwise</param>
        /// <returns>An awaitable task</returns>
        private Task OnExpire(string key, KeyValuePair<DateTime, object> item, bool deleting)
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
        private Task NotifyChangeListeners(string key, object item)
        {
            if (m_updateHandlers.ContainsKey(key))
                Task.Run(async () =>
                {
                    m_updateHandlers.TryGetValue(key, out var lst);
                    if (lst == null || lst.Count == 0)
                        return;

                    Func<string, Task>[] items;
                    using (await m_lock.LockAsync())
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
        public Task MonitorKeys(IEnumerable<string> keys, Func<string, Task> responder)
        {
            return m_lock.LockedAsync(() =>
            {
                foreach (var k in keys)
                {
                    if (!m_updateHandlers.TryGetValue(k, out var lst))
                        lst = m_updateHandlers[k] = new List<Func<string, Task>>();

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
        public Task UnmonitorKeys(IEnumerable<string> keys, Func<string, Task> responder)
        {
            return m_lock.LockedAsync(() =>
            {
                foreach (var k in keys)
                {
                    if (!m_updateHandlers.TryGetValue(k, out var lst))
                        continue;

                    lst.Remove(responder);
                    if (lst.Count == 0)
                        m_updateHandlers.Remove(k);
                }
            });
        }

        /// <summary>
        /// Registers a function to monitor a key
        /// </summary>
        /// <param name="keys">The key to monitor</param>
        /// <param name="responder">The function to invoke on changes</param>
        /// <returns>An awaitable task</returns>
        public Task MonitorKey(string key, Func<string, Task> responder)
        {
            return MonitorKeys(new string[] { key }, responder);
        }

        /// <summary>
        /// Unregisters a function to monitor a key
        /// </summary>
        /// <param name="keys">The key to unmonitor</param>
        /// <param name="responder">The function to remove</param>
        /// <returns>An awaitable task</returns>
        public Task UnmonitorKey(string key, Func<string, Task> responder)
        {
            return UnmonitorKeys(new string[] { key }, responder);
        }

        /// <summary>
        /// Adds an item to the cache
        /// </summary>
        /// <param name="key">The key to store the item under</param>
        /// <param name="item">The item to store</param>
        /// <returns>An awaitable task</returns>
        public Task AddItem(string key, object item)
        {
            return AddItem(key, item, DefaultExpirationTime.Ticks == 0 ? new DateTime(0) : DateTime.Now + DefaultExpirationTime);
        }

        /// <summary>
        /// Adds an item to the cache
        /// </summary>
        /// <param name="key">The key to store the item under</param>
        /// <param name="item">The item to store</param>
        /// <param name="expires">The time the item expires</param>
        /// <returns>An awaitable task</returns>
        public Task AddItem(string key, object item, TimeSpan expires)
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
        public async Task AddItem(string key, object item, DateTime expires)
        {
            await m_cache.AddOrReplaceAsync(key, new KeyValuePair<DateTime, object>(expires, item));
            await NotifyChangeListeners(key, item);
        }

        /// <summary>
        /// Removes an item
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>An awaitable task</returns>
        public Task RemoveItem(string key)
        {
            return m_cache.TryGetUnlessAsync(key, (a, b) => true);
        }

        /// <summary>
        /// Removes an item
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <returns>An awaitable task</returns>
        public async Task<object> TryGetValue(string key)
        {
            var exp = DateTime.Now;
            var item = await m_cache.TryGetUnlessAsync(key, (a, b) => b.Key.Ticks != 0 && b.Key < exp);
            return item.Key ? item.Value.Value : null;
        }

        /// <summary>
        /// Sets up the cache after configuration
        /// </summary>
        public void AfterConfigure()
        {
            if (m_cache != null)
                throw new InvalidOperationException("Can only initialize memcache once");

            m_cache = new LRUCache<KeyValuePair<DateTime, object>>(
                countlimit: MaxCacheItems,
                expirationHandler: OnExpire
            );

            // Register the first instance as the default instance
            if (_instances.Count == 0)
                _instances.Add(string.Empty, this);
            if (!string.IsNullOrWhiteSpace(Name))
                _instances.Add(Name, this);
        }

        /// <summary>
        /// Attempts to get the memcache with the given name
        /// </summary>
        /// <param name="name">The name of the instance to locate</param>
        /// <returns>The memcache instance or null</returns>
        public static MemCache GetByName(string name)
        {
            _instances.TryGetValue(name ?? string.Empty, out var m);
            return m;
        }

        /// <summary>
        /// Creates a typed helper bound to the given key
        /// </summary>
        /// <param name="key">The key to bind to</param>
        /// <typeparam name="T">The type to use</typeparam>
        /// <returns>The helper</returns>
        public static CacheHelperInstance<T> Helper<T>(string instance, string key)
            where T : class
            => (GetByName(instance) ?? throw new ArgumentException($"No memcache with the name: {instance}"))
                .Helper<T>(key);

        /// <summary>
        /// Creates a typed helper bound to the given key
        /// </summary>
        /// <param name="key">The key to bind to</param>
        /// <typeparam name="T">The type to use</typeparam>
        /// <returns>The helper</returns>
        public static CacheHelperInstanceWithSubkey<T> SubKeyHelper<T>(string instance, string key)
            where T : class
            => (GetByName(instance) ?? throw new ArgumentException($"No memcache with the name: {instance}"))
                .SubKeyHelper<T>(key);

        /// <summary>
        /// Creates a typed helper bound to the given key
        /// </summary>
        /// <param name="key">The key to bind to</param>
        /// <typeparam name="T">The type to use</typeparam>
        /// <returns>The helper</returns>
        public CacheHelperInstance<T> Helper<T>(string key)
            where T : class 
            => new CacheHelperInstance<T>(this, key);

        /// <summary>
        /// Creates a typed helper bound to the given key prefix
        /// </summary>
        /// <param name="key">The key prefix to bind to</param>
        /// <typeparam name="T">The type to use</typeparam>
        /// <returns>The helper</returns>
        public CacheHelperInstanceWithSubkey<T> SubKeyHelper<T>(string key)
            where T : class
            => new CacheHelperInstanceWithSubkey<T>(this, key);

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
            private readonly MemCache m_parent;

            /// <summary>
            /// Constructs a new CacheHelperInstance
            /// </summary>
            /// <param name="parent">The parent memcache instance</param>
            /// <param name="key">The key used to store the data</param>
            public CacheHelperInstance(MemCache parent, string key)
            {
                m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
                m_key = key ?? throw new ArgumentNullException(nameof(key));

            }

            /// <summary>
            /// Clears the key from the cache
            /// </summary>
            /// <returns>An awaitable task</returns>
            public Task InvalidateAsync() => m_parent.RemoveItem(m_key);
            /// <summary>
            /// Gets the value from the cache, or null
            /// </summary>
            /// <returns>The value or null</returns>
            public async Task<T> TryGetValueAsync() => (await m_parent.TryGetValue(m_key)) as T;
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
                if ((res = await m_parent.TryGetValue(m_key) as T) == null)
                    await m_parent.AddItem(m_key, res = await p());

                return res;
            }
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(T value) => m_parent.AddItem(m_key, value, m_parent.DefaultExpirationTime.Ticks == 0 ? new DateTime(0) : DateTime.Now + m_parent.DefaultExpirationTime);
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <param name="expires">The life-time of the entry</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(T value, TimeSpan expires) => m_parent.AddItem(m_key, value, expires);
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
            private readonly MemCache m_parent;

            /// <summary>
            /// Constructs a new CacheHelperInstanceWithSubkey
            /// </summary>
            /// <param name="parent">The memcache instance to use</param>
            /// <param name="key">The key used to store the data</param>
            public CacheHelperInstanceWithSubkey(MemCache parent, string key)
            {
                m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
                m_key = key ?? throw new ArgumentNullException(nameof(key));
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
            public Task InvalidateAsync(string subkey) => m_parent.RemoveItem(GetKeyForSubkey(subkey));
            /// <summary>
            /// Gets the value from the cache, or null
            /// </summary>
            /// <returns>The value or null</returns>
            public async Task<T> TryGetValueAsync(string subkey) => (await m_parent.TryGetValue(GetKeyForSubkey(subkey))) as T;
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
                if ((res = await m_parent.TryGetValue(GetKeyForSubkey(subkey)) as T) == null)
                    await m_parent.AddItem(GetKeyForSubkey(subkey), res = await p());

                return res;
            }
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(string subkey, T value) => m_parent.AddItem(GetKeyForSubkey(subkey), value, m_parent.DefaultExpirationTime.Ticks == 0 ? new DateTime(0) : DateTime.Now + m_parent.DefaultExpirationTime);
            /// <summary>
            /// Sets or updates the cached value
            /// </summary>
            /// <param name="value">The value to set</param>
            /// <param name="expires">The life-time of the entry</param>
            /// <returns>An awaitable task</returns>
            public Task SetValueAsync(string subkey, T value, TimeSpan expires) => m_parent.AddItem(GetKeyForSubkey(subkey), value, expires);
        }
    }
}