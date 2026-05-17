#nullable enable
namespace UniT.Pooling
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using UniT.Extensions;
    using UniT.Logging;
    using UniT.ResourceManagement;
    using UnityEngine;
    using UnityEngine.Scripting;
    using ILogger = UniT.Logging.ILogger;
    using Object = UnityEngine.Object;
    #if UNIT_UNITASK
    using System.Threading;
    using Cysharp.Threading.Tasks;
    #else
    using System.Collections;
    #endif

    public sealed class ObjectPoolManager : IObjectPoolManager
    {
        #region Constructor

        private readonly IAssetsManager assetsManager;
        private readonly ILogger        logger;

        private readonly Transform                          poolsContainer = new GameObject(nameof(ObjectPoolManager)).DontDestroyOnLoad().transform;
        private readonly Dictionary<object, GameObject>     keyToPrefab    = new();
        private readonly Dictionary<GameObject, ObjectPool> prefabToPool   = new();
        private readonly Dictionary<GameObject, ObjectPool> instanceToPool = new();

        [Preserve]
        public ObjectPoolManager(IAssetsManager assetsManager, ILoggerManager loggerManager)
        {
            this.assetsManager = assetsManager;
            this.logger        = loggerManager.GetLogger(this);
            this.logger.Debug("Constructed");
        }

        #endregion

        #region Public

        event Action<GameObject> IObjectPoolManager.Instantiated { add => this.instantiated += value; remove => this.instantiated -= value; }
        event Action<GameObject> IObjectPoolManager.Spawned      { add => this.spawned += value;      remove => this.spawned -= value; }
        event Action<GameObject> IObjectPoolManager.Recycled     { add => this.recycled += value;     remove => this.recycled -= value; }
        event Action<GameObject> IObjectPoolManager.CleanedUp    { add => this.cleanedUp += value;    remove => this.cleanedUp -= value; }

        void IObjectPoolManager.Load(GameObject prefab, int count) => this.Load(prefab, count);

        #if !UNITY_WEBGL
        void IObjectPoolManager.Load(object key, int count)
        {
            var prefab = this.keyToPrefab.GetOrAdd(key, static state => state.assetsManager.Load<GameObject>(state.key), (this.assetsManager, key));
            this.Load(prefab, count);
        }
        #endif

        #if UNIT_UNITASK
        async UniTask IObjectPoolManager.LoadAsync(object key, int count, IProgress<float>? progress, CancellationToken cancellationToken)
        {
            var prefab = await this.keyToPrefab.GetOrAddAsync(key, static state => state.assetsManager.LoadAsync<GameObject>(state.key, state.progress, state.cancellationToken), (this.assetsManager, key, progress, cancellationToken));
            this.Load(prefab, count);
        }
        #else
        IEnumerator IObjectPoolManager.LoadAsync(object key, int count, Action? callback, IProgress<float>? progress)
        {
            var prefab = default(GameObject)!;
            yield return this.keyToPrefab.GetOrAddAsync(
                key,
                callback => this.assetsManager.LoadAsync(key, callback, progress),
                result => prefab = result
            );
            this.Load(prefab, count);
            callback?.Invoke();
        }
        #endif

        GameObject IObjectPoolManager.Spawn(GameObject prefab, Vector3? position, Quaternion? rotation, Transform? parent, bool spawnInWorldSpace) => this.Spawn(prefab, position, rotation, parent, spawnInWorldSpace);

        GameObject IObjectPoolManager.Spawn(object key, Vector3? position, Quaternion? rotation, Transform? parent, bool spawnInWorldSpace)
        {
            var prefab = this.keyToPrefab.GetOrAdd(key, static state =>
            {
                #if !UNITY_WEBGL
                return state.assetsManager.Load<GameObject>(state.key);
                #else
                throw new NotSupportedException("Cannot directly Spawn with key on WebGL. Please preload it with `LoadAsync`.");
                #endif
            }, (this.assetsManager, key));
            return this.Spawn(prefab, position, rotation, parent, spawnInWorldSpace);
        }

        void IObjectPoolManager.Recycle(GameObject instance)
        {
            if (!this.instanceToPool.Remove(instance, out var pool)) throw new InvalidOperationException($"{instance.name} was not spawned from {nameof(ObjectPoolManager)}");
            pool.Recycle(instance);
            this.logger.Debug($"Recycled {instance.name}");
        }

        void IObjectPoolManager.RecycleAll(GameObject prefab) => this.RecycleAll(prefab);

        void IObjectPoolManager.RecycleAll(object key)
        {
            if (!this.TryGetPrefab(key, out var prefab)) return;
            this.RecycleAll(prefab);
        }

        void IObjectPoolManager.Cleanup(GameObject prefab, int retainCount) => this.Cleanup(prefab, retainCount);

        void IObjectPoolManager.Cleanup(object key, int retainCount)
        {
            if (!this.TryGetPrefab(key, out var prefab)) return;
            this.Cleanup(prefab, retainCount);
        }

        void IObjectPoolManager.Unload(GameObject prefab) => this.Unload(prefab);

        void IObjectPoolManager.Unload(object key)
        {
            if (!this.TryGetPrefab(key, out var prefab)) return;
            this.Unload(prefab);
            this.assetsManager.Unload(key);
            this.keyToPrefab.Remove(key);
        }

        #endregion

        #region Private

        private Action<GameObject>? instantiated;
        private Action<GameObject>? spawned;
        private Action<GameObject>? recycled;
        private Action<GameObject>? cleanedUp;

        private void Load(GameObject prefab, int count)
        {
            this.prefabToPool.GetOrAdd(prefab, static state =>
            {
                var pool = ObjectPool.Construct(state.prefab, state.@this.poolsContainer);
                pool.Instantiated += state.@this.OnInstantiated;
                pool.Spawned      += state.@this.OnSpawned;
                pool.Recycled     += state.@this.OnRecycled;
                pool.CleanedUp    += state.@this.OnCleanedUp;
                state.@this.logger.Debug($"Instantiated {pool.name}");
                return pool;
            }, (@this: this, prefab)).Load(count);
        }

        private GameObject Spawn(GameObject prefab, Vector3? position, Quaternion? rotation, Transform? parent, bool spawnInWorldSpace)
        {
            if (!this.prefabToPool.ContainsKey(prefab))
            {
                this.Load(prefab, 1);
                this.logger.Warning($"Auto loaded {prefab.name} pool. Consider preload it with `Load` or `LoadAsync` for better performance.");
            }
            var pool     = this.prefabToPool[prefab];
            var instance = pool.Spawn(position, rotation, parent, spawnInWorldSpace);
            this.instanceToPool.Add(instance, pool);
            this.logger.Debug($"Spawned {instance.name}");
            return instance;
        }

        private void RecycleAll(GameObject prefab)
        {
            if (!this.TryGetPool(prefab, out var pool)) return;
            pool.RecycleAll();
            this.instanceToPool.RemoveWhere((_, otherPool) => otherPool == pool);
            this.logger.Debug($"Recycled all {pool.name}");
        }

        private void Cleanup(GameObject prefab, int retainCount)
        {
            if (!this.TryGetPool(prefab, out var pool)) return;
            pool.Cleanup(retainCount);
            this.logger.Debug($"Cleaned up {pool.name}");
        }

        private void Unload(GameObject prefab)
        {
            if (!this.TryGetPool(prefab, out var pool)) return;
            pool.RecycleAll();
            pool.Cleanup(0);
            this.instanceToPool.RemoveWhere((_, otherPool) => otherPool == pool);
            pool.Instantiated -= this.OnInstantiated;
            pool.Spawned      -= this.OnSpawned;
            pool.Recycled     -= this.OnRecycled;
            pool.CleanedUp    -= this.OnCleanedUp;
            if (pool)
            {
                Object.Destroy(pool.gameObject);
                this.logger.Debug($"Destroyed {pool.name}");
            }
            this.prefabToPool.Remove(prefab);
        }

        private bool TryGetPool(GameObject prefab, [MaybeNullWhen(false)] out ObjectPool pool)
        {
            if (this.prefabToPool.TryGetValue(prefab, out pool)) return true;
            this.logger.Warning($"{prefab.name} pool not loaded");
            return false;
        }

        private bool TryGetPrefab(object key, [MaybeNullWhen(false)] out GameObject prefab)
        {
            if (this.keyToPrefab.TryGetValue(key, out prefab)) return true;
            this.logger.Warning($"{key} pool not loaded");
            return false;
        }

        private void OnInstantiated(GameObject instance) => this.instantiated?.Invoke(instance);
        private void OnSpawned(GameObject      instance) => this.spawned?.Invoke(instance);
        private void OnRecycled(GameObject     instance) => this.recycled?.Invoke(instance);
        private void OnCleanedUp(GameObject    instance) => this.cleanedUp?.Invoke(instance);

        #endregion

        #region Finalizer

        private void Dispose()
        {
            this.keyToPrefab.SafeForEach(this.Unload);
            this.prefabToPool.Keys.SafeForEach(this.Unload);
            if (this.poolsContainer) Object.Destroy(this.poolsContainer.gameObject);
        }

        void IDisposable.Dispose()
        {
            this.Dispose();
            this.logger.Debug("Disposed");
        }

        ~ObjectPoolManager()
        {
            this.Dispose();
            this.logger.Debug("Finalized");
        }

        #endregion
    }
}