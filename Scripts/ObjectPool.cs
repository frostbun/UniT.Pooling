#nullable enable
namespace UniT.Pooling
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UniT.Extensions;
    using UnityEngine;

    public sealed class ObjectPool : MonoBehaviour
    {
        #region Constructor

        [SerializeField] private GameObject prefab = null!;

        private readonly Stack<GameObject>   pooledObjects  = new();
        private readonly HashSet<GameObject> spawnedObjects = new();

        public static ObjectPool Construct(GameObject prefab, Transform parent)
        {
            var pool = new GameObject
            {
                name      = $"{prefab.name} pool",
                transform = { parent = parent },
            }.AddComponent<ObjectPool>();
            pool.prefab = prefab;
            return pool;
        }

        // ReSharper disable once InconsistentNaming
        public new Transform transform { get; private set; } = null!;

        private void Awake()
        {
            this.transform = base.transform;
            this.gameObject.SetActive(false);
        }

        #endregion

        #region Public

        public event Action<GameObject>? Instantiated;
        public event Action<GameObject>? Spawned;
        public event Action<GameObject>? Recycled;
        public event Action<GameObject>? CleanedUp;

        public void Load(int count)
        {
            while (this.pooledObjects.Count < count)
            {
                this.pooledObjects.Push(this.Instantiate());
            }
        }

        public GameObject Spawn(Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true)
        {
            var instance = this.pooledObjects.PopOrDefault(this.Instantiate);
            instance.transform.SetPositionAndRotation(position ?? Vector3.zero, rotation ?? Quaternion.identity);
            instance.transform.SetParent(parent, spawnInWorldSpace);
            instance.SetActive(true);
            this.spawnedObjects.Add(instance);
            this.Spawned?.Invoke(instance);
            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Spawn<T>(Vector3? position = null, Quaternion? rotation = null, Transform? parent = null, bool spawnInWorldSpace = true)
        {
            return this.Spawn(position, rotation, parent, spawnInWorldSpace).GetComponentOrThrow<T>();
        }

        public void Recycle(GameObject instance)
        {
            if (!this.spawnedObjects.Remove(instance)) throw new InvalidOperationException($"{instance.name} was not spawned from {this.name}");
            if (instance)
            {
                instance.transform.SetParent(this.transform, false);
                this.pooledObjects.Push(instance);
                this.Recycled?.Invoke(instance);
            }
            else
            {
                this.Recycled?.Invoke(instance!);
                this.CleanedUp?.Invoke(instance!);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Recycle<T>(T instance) where T : Component
        {
            this.Recycle(instance.gameObject);
        }

        public void RecycleAll()
        {
            this.spawnedObjects.SafeForEach(this.Recycle);
        }

        public void Cleanup(int retainCount = 1)
        {
            while (this.pooledObjects.Count > retainCount)
            {
                var instance = this.pooledObjects.Pop();
                if (instance) Destroy(instance);
                this.CleanedUp?.Invoke(instance!);
            }
        }

        #endregion

        #region Private

        private GameObject Instantiate()
        {
            var instance = Instantiate(this.prefab, this.transform);
            this.Instantiated?.Invoke(instance);
            return instance;
        }

        #endregion
    }
}