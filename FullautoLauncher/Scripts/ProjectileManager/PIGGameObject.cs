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
        base.Pool(par);
        par.Transform.gameObject.SetActive(false);
        par.Transform.parent = CustomProjectileManager.CustomProjectileParent;
    }

    public override void Update()
    {

    }

    protected override PHGameObject Create(ProjectileParams par)
    {
        var trans = item.CloneModel(GameManager.Instance.World, new ItemValue(item.Id), Vector3.zero, CustomProjectileManager.CustomProjectileParent);
        return new PHGameObject(trans, par);
    }
}

