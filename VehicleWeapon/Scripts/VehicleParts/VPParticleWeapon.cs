using System;
using System.Collections;
using UnityEngine;

public class VPParticleWeapon : VehicleWeaponBase
{
    protected float reloadTime = 1f;
    protected float reloadRemain = 0f;
    protected bool explodeOnCollision = true;
    protected bool explodeOnDeath = false;
    protected string reloadSound = string.Empty;
    protected ExplosionComponent component = null;
    protected ParticleSystem weaponSystem = null;
    protected SubExplosionInitializer initializer = null;
    ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();

    public ParticleSystem WeaponSystem { get => weaponSystem; }
    public ExplosionComponent Component { get => component; }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        string name = GetModName();
        reloadTime = 1f;
        properties.ParseFloat("reloadTime", ref reloadTime);
        reloadTime = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "reloadTime", reloadTime.ToString()));
        CustomExplosionManager.GetCustomParticleComponents(PlatformIndependentHash.StringToUInt16(vehicleValue.GetVehicleWeaponPropertyOverride(name, "particleIndex", properties.GetString("particleIndex"))), out component);

        explodeOnCollision = true;
        properties.ParseBool("explodeOnCollision", ref explodeOnCollision);
        explodeOnCollision = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "explodeOnCollision", explodeOnCollision.ToString()));
        explodeOnDeath = false;
        properties.ParseBool("explodeOnDeath", ref explodeOnDeath);
        explodeOnDeath = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "explodeOnDeath", explodeOnDeath.ToString()));

        properties.ParseString("reloadSound", ref reloadSound);
        reloadSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "reloadSound", reloadSound);

        reloadRemain = reloadTime;
    }

    public override void InitPrefabConnections()
    {
        Transform hornTrans = GetParticleTransform();
        if (hornTrans)
        {
            weaponSystem = hornTrans.GetComponent<ParticleSystem>();
            if (weaponSystem)
            {
                var emission = weaponSystem.emission;
                emission.enabled = false;
                weaponSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        base.InitPrefabConnections();
    }

    public override void NoPauseUpdate(float _dt)
    {
        base.NoPauseUpdate(_dt);
        if (!IsCoRunning && reloadRemain > 0)
            reloadRemain -= _dt;
    }

    public override void OnPlayerEnter()
    {
        base.OnPlayerEnter();
        initializer = weaponSystem.gameObject.AddComponent<SubExplosionInitializer>();
        initializer.data = component.BoundExplosionData;
        initializer.entityAlive = player;
        if (component.BoundItemClass != null)
            initializer.value = new ItemValue(component.BoundItemClass.Id);
        if (explodeOnDeath)
            initializer.SetExplodeOnDeath(explodeOnCollision);
    }

    public override void OnPlayerDetach()
    {
        base.OnPlayerDetach();
        if (initializer)
        {
            GameObject.Destroy(initializer);
            initializer = null;
        }
    }

    protected internal override bool CanFire(bool firstShot, bool isRelease, bool fromSlot)
    {
        if (isRelease)
        {
            pressed = false;
            return false;
        }
        else if (pressed && !fullauto)
            return false;

        if (weaponSystem.gameObject.activeInHierarchy && base.CanFire(firstShot, isRelease, fromSlot))
        {
            if(ammoValue.type > 0 && player.bag.GetItemCount(ammoValue) < burstRepeat)
            {
                if(!pressed)
                    vehicle.entity.PlayOneShot(emptySound);
                pressed = true;
                return false;
            }
            if(IsCoRunning || reloadRemain > 0)
            {
                if(!pressed)
                    vehicle.entity.PlayOneShot(notReadySound);
                pressed = true;
                return false;
            }

            pressed = true;
            return true;
        }
        return false;
    }

    protected internal override void OnBurstShot()
    {
        base.OnBurstShot();
        component.BoundItemClass.FireEvent(MinEventTypes.onSelfRangedBurstShot, player.MinEventContext);
        reloadRemain = reloadTime;
    }

    protected override void OnFireEnd()
    {
        vehicle.entity.PlayOneShot(reloadSound);
    }

    public override void NetFireWrite(PooledBinaryWriter _bw)
    {
        base.NetFireWrite(_bw);
        _bw.Write((uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue));
    }

    public override void NetFireRead(PooledBinaryReader _br)
    {
        base.NetFireRead(_br);
        uint seed = _br.ReadUInt32();
        if(weaponSystem)
        {
            param.randomSeed = seed;
            weaponSystem.Emit(param, burstCount);
        }
        reloadRemain = reloadTime;
    }
}
