using System;
using System.Collections;
using UnityEngine;

public class VPParticleWeapon : VehicleWeaponBase
{
    protected int burstCount = 1;
    protected int burstRepeat = 1;
    protected float burstInterval = 0f;
    protected float reloadTime = 1f;
    protected float reloadRemain = 0f;
    protected float burstDelay = 0f;
    protected bool fullauto = false;
    protected bool explodeOnCollision = true;
    protected bool explodeOnDeath = false;
    protected string emptySound = string.Empty;
    protected string reloadSound = string.Empty;
    protected string fireSound = string.Empty;
    protected ExplosionComponent component = null;
    protected ParticleSystem weaponSystem = null;
    protected SubExplosionInitializer initializer = null;
    protected bool isCoRunning = false;
    protected ItemValue ammoValue = ItemValue.None.Clone();

    public ParticleSystem WeaponSystem { get => weaponSystem; }
    public ExplosionComponent Component { get => component; }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        string name = GetModName();
        base.ApplyModEffect(vehicleValue);
        burstCount = 1;
        properties.ParseInt("burstCount", ref burstCount);
        burstCount = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstCount", burstCount.ToString()));
        burstInterval = 0f;
        properties.ParseFloat("burstInterval", ref burstInterval);
        burstInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstInterval", burstInterval.ToString()));
        burstRepeat = 1;
        properties.ParseInt("burstRepeat", ref burstRepeat);
        burstRepeat = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstRepeat", burstRepeat.ToString()));
        reloadTime = 1f;
        properties.ParseFloat("reloadTime", ref reloadTime);
        reloadTime = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "reloadTime", reloadTime.ToString()));
        burstDelay = 0f;
        properties.ParseFloat("burstDelay", ref burstDelay);
        burstDelay = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstDelay", burstDelay.ToString()));
        fullauto = false;
        properties.ParseBool("fullauto", ref fullauto);
        fullauto = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "fullauto", fullauto.ToString()));

        CustomExplosionManager.GetCustomParticleComponents(PlatformIndependentHash.StringToUInt16(vehicleValue.GetVehicleWeaponPropertyOverride(name, "particleIndex", properties.GetString("particleIndex"))), out component);

        explodeOnCollision = true;
        properties.ParseBool("explodeOnCollision", ref explodeOnCollision);
        explodeOnCollision = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "explodeOnCollision", explodeOnCollision.ToString()));
        explodeOnDeath = false;
        properties.ParseBool("explodeOnDeath", ref explodeOnDeath);
        explodeOnDeath = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "explodeOnDeath", explodeOnDeath.ToString()));

        string str = null;
        ammoValue = ItemValue.None.Clone();
        properties.ParseString("ammo", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(name, "ammo", str);
        if (!string.IsNullOrEmpty(str))
            ammoValue = ItemClass.GetItem(str, false);

        properties.ParseString("emptySound", ref emptySound);
        emptySound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "emptySound", emptySound);
        properties.ParseString("reloadSound", ref reloadSound);
        reloadSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "reloadSound", reloadSound);
        properties.ParseString("fireSound", ref fireSound);
        fireSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "fireSound", fireSound);

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
        if (!isCoRunning && reloadRemain > 0)
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
            if(isCoRunning || reloadRemain > 0)
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

    protected internal override void DoFire()
    {
        base.DoFire();
        if (burstInterval > 0 || burstDelay > 0)
            ThreadManager.StartCoroutine(DoParticleFireCo());
        else
            DoParticleFireNow();
    }

    protected internal override void Fired()
    {
        base.Fired();
        reloadRemain = reloadTime;
    }

    protected virtual IEnumerator DoParticleFireCo()
    {
        isCoRunning = true;
        if(burstDelay > 0)
            yield return new WaitForSecondsRealtime(burstDelay);
        if (burstInterval > 0)
        {
            int curBurstCount = 0;
            while (curBurstCount < burstRepeat)
            {
                if (!hasOperator)
                    break;
                DoParticleFireServer(burstCount);
                ++curBurstCount;
                yield return new WaitForSecondsRealtime(burstInterval);
            }
            vehicle.entity.PlayOneShot(reloadSound);
        }
        else
            DoParticleFireNow();

        isCoRunning = false;
        yield break;
    }

    protected virtual void DoParticleFireNow()
    {
        for (int i = 0; i < burstRepeat; ++i)
            DoParticleFireServer(burstCount);
        vehicle.entity.PlayOneShot(reloadSound);
    }

    protected virtual void DoParticleFireServer(int count)
    {
        uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageParticleWeaponFire>().Setup(vehicle.entity.entityId, (rotator != null && rotator.HorRotTrans != null) ? rotator.HorRotTrans.localEulerAngles.y : 0, (rotator != null && rotator.VerRotTrans != null) ? rotator.VerRotTrans.localEulerAngles.x : 0, seat, slot, UserData, count, seed));
        else
        {
            if(SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageParticleWeaponFire>().Setup(vehicle.entity.entityId, (rotator != null && rotator.HorRotTrans != null) ? rotator.HorRotTrans.localEulerAngles.y : 0, (rotator != null && rotator.VerRotTrans != null) ? rotator.VerRotTrans.localEulerAngles.x : 0, seat, slot, UserData, count, seed));
            DoParticleFireClient(count, seed);
        }
        PlayFiringSound();
        if (ammoValue.type > 0)
            ConsumeAmmo(1);
    }

    public virtual void DoParticleFireClient(int count, uint seed)
    {
        if(weaponSystem)
        {
            ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();
            param.randomSeed = seed;
            weaponSystem.Emit(param, count);
        }
        reloadRemain = reloadTime;
    }

    protected virtual void ConsumeAmmo(int count)
    {
        player.bag.DecItem(ammoValue, count);
    }

    protected virtual void PlayFiringSound()
    {
        vehicle.entity.PlayOneShot(fireSound, false);
    }
}
