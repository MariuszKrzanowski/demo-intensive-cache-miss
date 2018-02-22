using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MrMatrix.Net.IntensiveCacheMiss
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey">The type of the key. Key value is used to ask external system.</typeparam>
    /// <typeparam name="TResult">The type of the result. Result value returned from external system.</typeparam>
    public class CacheResolver<TKey, TResult>
    {

        /// <summary>
        /// The dictionary grouping pending requests from external systems by a key.
        /// </summary>
        private Dictionary<TKey, SingleKeyResolver> _dictionary = new Dictionary<TKey, SingleKeyResolver>();

        /// <summary>
        /// To avoid parallel access pessimistic lock is introduced.
        /// Each request is identified by a cache key. 
        /// Access to the dictionary is synchronized.
        /// Only operation of adding and taking single key process handler is atomic. 
        /// </summary>
        private SemaphoreSlim _lock = new SemaphoreSlim(1);

        /// <summary>
        /// Method assigns process responsible of retrieving data for specified key. 
        /// When method is called multiple times with same key 
        /// only first dataSupplier function/task will be executed.  
        /// </summary>
        /// <param name="key">The key, to identify external resource.</param>
        /// <param name="dataSupplier">The data supplier. Function responsible for retrieving data from external systems. 
        /// Function can call cache, call external system, update cache.</param>
        /// <returns></returns>
        public async Task<TResult> TakeResourceAsync(
            TKey key,
            Func<TKey, Task<TResult>> dataSupplier)
        {
            SingleKeyResolver singleKeyResolver = null;
            await _lock.WaitAsync();
            if (_dictionary.ContainsKey(key))
            {
                singleKeyResolver = _dictionary[key];
            }
            else
            {
                _dictionary[key] = singleKeyResolver = new SingleKeyResolver(key, this);
            }
            _lock.Release();
            return await singleKeyResolver.GetResourceAsync(dataSupplier).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes the single key resolver. 
        /// When process of retrieving data for specific key is finished. 
        /// Assigned process resolver is removed from the dictionary. 
        /// NOTE: All registered requests should have access, already have a task. 
        /// </summary>
        /// <param name="key">The key used to identify assigned resolver.</param>
        /// <param name="skr">The single key resolver instance to be removed.</param>
        /// <returns>Task with information when key is removed from dictionary.</returns>
        private async Task RemoveSingleKeyResolverAsync(TKey key, SingleKeyResolver skr)
        {
            await _lock.WaitAsync();
            if (_dictionary.ContainsKey(key) && Object.ReferenceEquals(skr, _dictionary[key]))
            {
                _dictionary.Remove(key);
            }
            _lock.Release();
        }

        /// <summary>
        /// 
        /// </summary>
        private class SingleKeyResolver
        {
            /// <summary>
            /// The key
            /// </summary>
            private TKey _key;
            /// <summary>
            /// The parent
            /// </summary>
            private CacheResolver<TKey, TResult> _parent;
            /// <summary>
            /// The result resolver
            /// </summary>
            private Task<TResult> _resultResolver;
            /// <summary>
            /// The cleanup task
            /// </summary>
            private Task _cleanupTask;
            /// <summary>
            /// The lock
            /// </summary>
            private SemaphoreSlim _lock;


            /// <summary>
            /// Initializes a new instance of the <see cref="SingleKeyResolver"/> class.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="parent">The parent.</param>
            public SingleKeyResolver(TKey key, CacheResolver<TKey, TResult> parent)
            {
                _key = key;
                _parent = parent;
                _resultResolver = null;
                _lock = new SemaphoreSlim(1);
            }

            /// <summary>
            /// Gets the resource.
            /// </summary>
            /// <param name="dataSupplier">The data supplier.</param>
            /// <returns></returns>
            public async Task<TResult> GetResourceAsync(
                Func<TKey, Task<TResult>> dataSupplier)
            {
                await _lock.WaitAsync();
                if (_resultResolver == null)
                {
                    _resultResolver = dataSupplier(_key);
                    _cleanupTask = _resultResolver.ContinueWith(
                        async (_) => await _parent.RemoveSingleKeyResolverAsync(this._key, this).ConfigureAwait(false)
                        );
                }
                _lock.Release();
                await _cleanupTask.ConfigureAwait(false);
                TResult result = await _resultResolver.ConfigureAwait(false);
                return result;
            }
        }

    }
}
