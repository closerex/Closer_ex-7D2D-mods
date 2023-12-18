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
    }

    public abstract class ProjectileItemGroupAbs<T> : IProjectileItemGroup where T : ParameterHolderAbs
    {
        protected readonly Queue<T> queue_pool = new Queue<T>();
        protected readonly Dictionary<int, HashSet<T>> dict_fired_projectiles = new Dictionary<int, HashSet<T>>();
        protected readonly ItemClass item;
        protected readonly int maxPoolCount = 1000;
        private int nextID;
        private int NextID { get => nextID++; }

        public ProjectileItemGroupAbs(ItemClass item)
        {
            this.item = item;
        }

        protected abstract T Create(ProjectileParams par);

        public virtual void Cleanup()
        {
        }

        public void Pool(int count)
        {
            count = Mathf.Min(maxPoolCount - queue_pool.Count, count - queue_pool.Count);
            for (int i = 0; i < count; i++)
            {
                queue_pool.Enqueue(Create(new ProjectileParams(NextID)));
            }
        }

        public virtual void Pool(T par)
        {
            if (maxPoolCount > queue_pool.Count)
            {
                par.Params.bOnIdealPosition = false;
                queue_pool.Enqueue(par);
            }
        }

        public ProjectileParams Fire(int entityID, ProjectileParams.ItemInfo info, Vector3 _idealStartPosition, Vector3 _realStartPosition, Vector3 _flyDirection, Entity _firingEntity, int _hmOverride = 0, float _radius = 0f)
        {
            T par = queue_pool.Count == 0 ? Create(new ProjectileParams(NextID)) : queue_pool.Dequeue();
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
