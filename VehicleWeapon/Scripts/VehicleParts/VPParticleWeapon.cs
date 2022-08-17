using System;
using System.Collections;
using UnityEngine;

public class VPParticleWeapon : VehicleWeaponBase
{
    protected bool explodeOnCollision = true;
    protected bool explodeOnDeath = false;
    protected string reloadSound = string.Empty;
    protected ExplosionComponent component = null;
    protected ParticleSystem weaponSystem = null;
    protected SubExplosionInitializer initializer = null;
    ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();

    private bool coFireQueuedThisFrame = false;
    private Coroutine coInstance = null;

    public ParticleSystem WeaponSystem { get => weaponSystem; }
    public ExplosionComponent Component { get => component; }
    public override bool IsBurstPending => coInstance != null;

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        repeatInterval = 1f;
        properties.ParseFloat("reloadTime", ref repeatInterval);
        repeatInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "reloadTime", repeatInterval.ToString()));
        CustomExplosionManager.GetCustomParticleComponents(CustomExplosionManager.getHashCode(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "particleIndex", properties.GetString("particleIndex"))), out component);

        explodeOnCollision = true;
        properties.ParseBool("explodeOnCollision", ref explodeOnCollision);
        explodeOnCollision = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "explodeOnCollision", explodeOnCollision.ToString()));
        explodeOnDeath = false;
        properties.ParseBool("explodeOnDeath", ref explodeOnDeath);
        explodeOnDeath = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "explodeOnDeath", explodeOnDeath.ToString()));

        reloadSound = String.Empty;
        properties.ParseString("reloadSound", ref reloadSound);
        reloadSound = vehicleValue.GetVehicleWeaponPropertyOverride(ModName, "reloadSound", reloadSound);

        repeatCooldown = repeatInterval;
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

        if (coFireQueuedThisFrame)
        {
            coFireQueuedThisFrame = false;
            if (burstInterval > 0)
                NetSyncFire(NextFiringState);
            else
                DoFireNow();
        }
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

        if(repeatCooldown > 0)
            vehicle.entity.PlayOneShot(reloadSound);
    }

    public override void OnPlayerDetach()
    {
        base.OnPlayerDetach();
        if (initializer)
        {
            GameObject.Destroy(initializer);
            initializer = null;
        }
        if (repeatCooldown > 0)
            repeatCooldown = repeatInterval;
    }

    protected internal override void OnDeactivated()
    {
        base.OnDeactivated();

        coFireQueuedThisFrame = false;
        if (coInstance != null)
        {
            ThreadManager.StopCoroutine(coInstance);
            coInstance = null;
        }
    }

    protected internal override void DoFire()
    {
        if (burstInterval > 0 || burstDelay > 0)
            coInstance = ThreadManager.StartCoroutine(DoFireCo());
        else
            DoFireNow();
    }

    protected IEnumerator DoFireCo()
    {
        if (burstDelay > 0)
            yield return new WaitForSecondsRealtime(burstDelay);
        if (burstInterval > 0)
        {
            int curBurstCount = 0;
            while (curBurstCount < burstRepeat)
            {
                if (!hasOperator || !activated || !enabled)
                {
                    coFireQueuedThisFrame = false;
                    break;
                }
                if (GameManager.Instance.IsPaused())
                    yield return new WaitUntil(() => { return !GameManager.Instance.IsPaused(); });
                coFireQueuedThisFrame = true;
                yield return null;
                ++curBurstCount;
                yield return new WaitForSecondsRealtime(burstInterval);
            }
        }
        else
            coFireQueuedThisFrame = true;

        coInstance = null;
        OnFireEnd();
        NetSyncFire(FiringState.Stop);
        yield break;
    }

    protected internal override void OnBurstShot()
    {
        base.OnBurstShot();
        player.MinEventContext.ItemValue = initializer.value;
        component.BoundItemClass.FireEvent(MinEventTypes.onSelfRangedBurstShotStart, player.MinEventContext);
    }

    protected override void OnFireEnd()
    {
        base.OnFireEnd();
        vehicle.entity.PlayOneShot(reloadSound);
    }

    public override void NetFireWrite(PooledBinaryWriter _bw, VehicleWeaponBase.FiringState state)
    {
        base.NetFireWrite(_bw, state);
        if(state != FiringState.Stop)
            _bw.Write((uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue));
    }

    public override void NetFireRead(PooledBinaryReader _br, VehicleWeaponBase.FiringState state)
    {
        base.NetFireRead(_br, state);
        if(state != FiringState.Stop)
        {
            uint seed = _br.ReadUInt32();
            if(weaponSystem)
            {
                param.randomSeed = seed;
                weaponSystem.Emit(param, burstCount);
            }
        }
    }
}
