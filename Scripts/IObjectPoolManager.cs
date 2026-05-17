#nullable enable
namespace UniT.Pooling
{
    using System;
    using System.Runtime.CompilerServices;
    using UniT.Extensions;
    using UnityEngine;
    #if UNIT_UNITASK
    using System.Threading;
    using Cysharp.Threading.Tasks;
    #else
    using System.Collections;
    #endif

    public interface IObjectPoolManager : IDisposable
    {
        public event Action<GameObject> Instantiated;

        public event Action<GameObject> Spawned;

        public event Action<GameObject> Recycled;

        public event Action<GameObject> CleanedUp;

        public void Load(GameObject prefab, int count = 1);

        #if !UNITY_WEBGL
        public void Load(object key, int count = 1);
        #endif

        public GameObject Spawn(GameObject prefab, Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true);

        public GameObject Spawn(object key, Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true);

        public void Recycle(GameObject instance);

        public void RecycleAll(GameObject prefab);

        public void RecycleAll(object key);

        public void Cleanup(GameObject prefab, int retainCount = 1);

        public void Cleanup(object key, int retainCount = 1);

        public void Unload(GameObject prefab);

        public void Unload(object key);

        #region Component

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Load(Component prefab, int count = 1) => this.Load(prefab.gameObject, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>(T prefab, Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true) where T : Component => this.Spawn(prefab.gameObject, position, rotation, parent, spawnInWorldSpace).GetComponent<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>(object key, Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true) => this.Spawn(key, position, rotation, parent, spawnInWorldSpace).GetComponentOrThrow<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Recycle(Component instance) => this.Recycle(instance.gameObject);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleAll(Component prefab) => this.RecycleAll(prefab.gameObject);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Cleanup(Component prefab, int retainCount = 1) => this.Cleanup(prefab.gameObject, retainCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unload(Component prefab) => this.Unload(prefab.gameObject);

        #endregion

        #region Implicit Key

        #if !UNITY_WEBGL
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Load<T>(int count = 1) => this.Load(typeof(T).GetKey(), count);
        #endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>(Vector3? position = null, Quaternion? rotation = null, Transform? parent = null) => this.Spawn<T>(typeof(T).GetKey(), position, rotation, parent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleAll<T>() => this.RecycleAll(typeof(T).GetKey());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Cleanup<T>(int retainCount = 1) => this.Cleanup(typeof(T).GetKey(), retainCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unload<T>() => this.Unload(typeof(T).GetKey());

        #endregion

        #region Async

        #if UNIT_UNITASK
        public UniTask LoadAsync(object key, int count = 1, IProgress<float>? progress = null, CancellationToken cancellationToken = default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniTask LoadAsync<T>(int count = 1, IProgress<float>? progress = null, CancellationToken cancellationToken = default) => this.LoadAsync(typeof(T).GetKey(), count, progress, cancellationToken);
        #else
        public IEnumerator LoadAsync(object key, int count = 1, Action? callback = null, IProgress<float>? progress = null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator LoadAsync<T>(int count = 1, Action? callback = null, IProgress<float>? progress = null) => this.LoadAsync(typeof(T).GetKey(), count, callback, progress);
        #endif

        #endregion
    }
}