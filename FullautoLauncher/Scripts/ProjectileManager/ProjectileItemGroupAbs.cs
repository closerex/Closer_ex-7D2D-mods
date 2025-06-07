using System.Collections.Generic;
using UnityEngine;

namespace FullautoLauncher.Scripts.ProjectileManager
{
    public interface IProjectileItemGroup
    {
        void Pool(int count);
        ProjectileParams Fire(int entityID, ProjectileParams.ItemInfo info, Vector3 _idealStartPosition, Vector3 _realStartPosition, Vector3 _flyDirection, Entity _firingEntity, int _hmOverride = 0, float _radius = 0f);
        void Update();
        void FixedUpdate();
        void Cleanup();
        Transform GetStickyTransform();
        void PoolStickyTransform(Transform stockTransform);
    }

    public abstract class ProjectileItemGroupAbs<T> : IProjectileItemGroup where T : ParameterHolderAbs
    {
        protected readonly Queue<T> queue_pool_projectile = new Queue<T>();
        protected readonly Queue<Transform> queue_pool_sticky = new Queue<Transform>();
        protected readonly Dictionary<int, HashSet<T>> dict_fired_projectiles = new Dictionary<int, HashSet<T>>();
        protected readonly ItemClass item;
        protected readonly int maxPoolCount = 1000;
        protected readonly int maxStickyCount = 500;
        private int nextID;
        private int NextID { get => nextID++; }

        public ProjectileItemGroupAbs(ItemClass item)
        {
            this.item = item;
        }

        protected abstract T Create(ProjectileParams par);

        private Transform CreateStickyTransform()
        {
            var trans = item.CloneModel(GameManager.Instance.World, new ItemValue(item.Id), Vector3.zero, CustomProjectileManager.CustomProjectileParent);
            Utils.SetLayerRecursively(trans.gameObject, 13);
            trans.gameObject.AddComponent<ProjectileMoveScript>().SetState(ProjectileMoveScript.State.Sticky);
            return trans;
        }

        public Transform GetStickyTransform()
        {
            if (queue_pool_sticky.Count == 0)
            {
                return CreateStickyTransform();
            }
            return queue_pool_sticky.Dequeue();
        }

        public void PoolStickyTransform(Transform sticky)
        {
            if (sticky == null || !sticky.gameObject.activeSelf)
            {
                return;
            }
            sticky.gameObject.SetActive(false);
            sticky.parent = CustomProjectileManager.CustomProjectileParent;
            queue_pool_sticky.Enqueue(sticky);
        }

        public virtual void Cleanup()
        {
            foreach (var sticky in queue_pool_sticky)
            {
                if (sticky != null)
                {
                    Object.Destroy(sticky.gameObject);
                }
            }
            queue_pool_sticky.Clear();
        }

        public void Pool(int count)
        {
            int poolCount = Mathf.Min(maxPoolCount - queue_pool_projectile.Count, count - queue_pool_projectile.Count);
            for (int i = 0; i < poolCount; i++)
            {
                queue_pool_projectile.Enqueue(Create(new ProjectileParams(NextID)));
            }
            if (item.IsSticky)
            {
                int stickyCount = Mathf.Min(maxStickyCount - queue_pool_sticky.Count, count - queue_pool_sticky.Count);
                for (int i = 0; i < stickyCount; i++)
                {
                    Transform sticky = CreateStickyTransform();
                    queue_pool_sticky.Enqueue(sticky);
                }
            }
        }

        public virtual void Pool(T par)
        {
            if (maxPoolCount > queue_pool_projectile.Count)
            {
                par.Params.bOnIdealPosition = false;
                queue_pool_projectile.Enqueue(par);
            }
        }

        public ProjectileParams Fire(int entityID, ProjectileParams.ItemInfo info, Vector3 _idealStartPosition, Vector3 _realStartPosition, Vector3 _flyDirection, Entity _firingEntity, int _hmOverride = 0, float _radius = 0f)
        {
            T par = queue_pool_projectile.Count == 0 ? Create(new ProjectileParams(NextID)) : queue_pool_projectile.Dequeue();
            if(!dict_fired_projectiles.TryGetValue(entityID, out HashSet<T> set))
            {
                set = new HashSet<T>();
                dict_fired_projectiles.Add(entityID, set);
            }
            set.Add(par);
            par.Params.Fire(info, _idealStartPosition, _realStartPosition, _flyDirection, _firingEntity, _hmOverride, _radius);
            par.Fire();
            return par.Params;
        }

        public abstract void Update();

        private List<T> list_remove = new List<T>();
        public void FixedUpdate()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsPaused() || GameManager.Instance.World == null)
                return;
            foreach (var pair in dict_fired_projectiles)
            {
                EntityAlive entityAlive = GameManager.Instance.World.GetEntity(pair.Key) as EntityAlive;

                list_remove.Clear();
                foreach (var projectile in pair.Value)
                {
                    if (projectile.Params.UpdatePosition())
                    {
                        list_remove.Add(projectile);
                    }
                    else
                    {
                        projectile.UpdatePosition();
                    }
                }

                if (entityAlive != null)
                {
                    int prevLayer = 0;
                    if (entityAlive.emodel != null)
                    {
                        prevLayer = entityAlive.GetModelLayer();
                        entityAlive.SetModelLayer(2, false, null);
                    }
                    foreach (var projectile in pair.Value)
                    {
                        if (projectile.Params.CheckCollision(entityAlive))
                        {
                            list_remove.Add(projectile);
                        }
                    }
                    if (entityAlive.emodel != null)
                    {
                        entityAlive.SetModelLayer(prevLayer, false, null);
                    }
                }

                foreach (var remove in list_remove)
                {
                    pair.Value.Remove(remove);
                    Pool(remove);
                }
                list_remove.Clear();
            }
        }
    }
}
