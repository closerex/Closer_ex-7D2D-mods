using FullautoLauncher.Scripts.ProjectileManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PIGGameObject : ProjectileItemGroupAbs<PHGameObject>
{
    public PIGGameObject(ItemClass item) : base(item)
    {
    }

    public override void Cleanup()
    {
        base.Cleanup();
        foreach (var proj in queue_pool_projectile)
        {
            proj.Dispose();
        }
        queue_pool_projectile.Clear();

        foreach (var set in dict_fired_projectiles.Values)
        {
            foreach (var proj in set)
            {
                proj.Dispose();
            }
        }
        dict_fired_projectiles.Clear();
    }

    public override void Pool(PHGameObject par)
    {
        if (queue_pool_projectile.Count >= maxPoolCount)
        {
            par.Dispose();
            return;
        }
        base.Pool(par);
        par.Transform.gameObject.SetActive(false);
        par.Transform.parent = CustomProjectileManager.CustomProjectileParent;
    }

    public override void Update()
    {

    }

    protected override PHGameObject Create(ProjectileParams par)
    {
        var handle = item.CloneModel(new ItemValue(item.Id), CustomProjectileManager.CustomProjectileParent, BlockShape.MeshPurpose.World);
        if (!handle.Instance)
        {
            throw new InvalidOperationException($"Cannot create projectile model for {item.Name}.");
        }
        return new PHGameObject(handle, par);
    }
}

