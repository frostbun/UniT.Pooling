#nullable enable
namespace UniT.Pooling
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UniT.Extensions;
    using UnityEngine;

    public interface IObjectPoolManager : IDisposable
    {
        public event Action<GameObject> Instantiated;

        public event Action<GameObject> Spawned;

        public event Action<GameObject> Recycled;

        public event Action<GameObject> CleanedUp;

        public void Load(GameObject prefab, int count = 1);

        public UniTask LoadAsync(object key, int count = 1, IProgress<float>? progress = null, CancellationToken cancellationToken = default);

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
        public T Spawn<T>(GameObject prefab, Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true) where T : notnull => this.Spawn(prefab, position, rotation, parent, spawnInWorldSpace).GetComponentOrThrow<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>(object key, Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true) where T : notnull => this.Spawn(key, position, rotation, parent, spawnInWorldSpace).GetComponentOrThrow<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Recycle(Component instance)
        {
            if (!instance) return;
            this.Recycle(instance.gameObject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleAll(Component prefab) => this.RecycleAll(prefab.gameObject);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Cleanup(Component prefab, int retainCount = 1) => this.Cleanup(prefab.gameObject, retainCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unload(Component prefab) => this.Unload(prefab.gameObject);

        #endregion

        #region Implicit Key

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UniTask LoadAsync<T>(int count = 1, IProgress<float>? progress = null, CancellationToken cancellationToken = default) where T : notnull => this.LoadAsync(typeof(T).GetKey(), count, progress, cancellationToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>(Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true) where T : notnull => this.Spawn<T>(typeof(T).GetKey(), position, rotation, parent, spawnInWorldSpace);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleAll<T>() where T : notnull => this.RecycleAll(typeof(T).GetKey());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Cleanup<T>(int retainCount = 1) where T : notnull => this.Cleanup(typeof(T).GetKey(), retainCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unload<T>() where T : notnull => this.Unload(typeof(T).GetKey());

        #endregion
    }
}