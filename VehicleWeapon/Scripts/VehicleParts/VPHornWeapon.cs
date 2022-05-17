using System;
using System.Collections;
using UnityEngine;

public class VPHornWeapon : VPWeaponBase
{
    protected int burstCount = 1;
    protected int burstRepeat = 1;
    protected float burstInterval = 0f;
    protected float hornInterval = 1f;
    protected float hornCooldown = 0f;
    protected bool explodeOnCollision = true;
    protected bool explodeOnDeath = false;
    protected string hornEmptySound = string.Empty;
    protected string hornReloadSound = string.Empty;
    protected string hornFireSound = string.Empty;
    protected CustomParticleComponents component = null;
    protected ParticleSystem hornSystem = null;
    protected SubExplosionInitializer initializer = null;
    protected bool isCoRunning = false;
    protected ItemValue ammoValue = ItemValue.None.Clone();

    public ParticleSystem HornSystem { get => hornSystem; }
    public CustomParticleComponents Component { get => component; }
    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        _properties.ParseInt("burstCount", ref burstCount);
        _properties.ParseFloat("burstInterval", ref burstInterval);
        _properties.ParseInt("burstRepeat", ref burstRepeat);
        _properties.ParseFloat("hornInterval", ref hornInterval);
        hornCooldown = 0;

        string str = null;
        _properties.ParseString("particleIndex", ref str);
        if (!string.IsNullOrEmpty(str))
            CustomParticleEffectLoader.GetCustomParticleComponents(PlatformIndependentHash.StringToUInt16(str), out component);
        _properties.ParseBool("explodeOnCollision", ref explodeOnCollision);
        _properties.ParseBool("explodeOnDeath", ref explodeOnDeath);
        str = null;
        _properties.ParseString("ammo", ref str);
        if (!string.IsNullOrEmpty(str))
            ammoValue = ItemClass.GetItem(str, false);

        _properties.ParseString("emptySound", ref hornEmptySound);
        _properties.ParseString("reloadSound", ref hornReloadSound);
        hornFireSound = vehicle.GetHornSoundName();
        _properties.ParseString("hornSound", ref hornFireSound);
    }

    public override void InitPrefabConnections()
    {
        Transform hornTrans = GetParticleTransform();
        if (hornTrans)
        {
            hornSystem = hornTrans.GetComponent<ParticleSystem>();
            if (hornSystem)
            {
                var emission = hornSystem.emission;
                emission.enabled = false;
            }
        }
        base.InitPrefabConnections();
    }


    public override void Update(float _dt)
    {
        if (!isCoRunning && hornCooldown > 0)
            hornCooldown -= _dt;

        base.Update(_dt);
    }

    protected override void OnPlayerEnter()
    {
        base.OnPlayerEnter();
        initializer = hornSystem.gameObject.AddComponent<SubExplosionInitializer>();
        initializer.data = component.BoundExplosionData;
        initializer.entityAlive = player;
        if (component.BoundItemClass != null)
            initializer.value = new ItemValue(component.BoundItemClass.Id);
        if (explodeOnDeath)
            initializer.SetExplodeOnDeath(explodeOnCollision);
    }

    protected override void OnPlayerDetach()
    {
        base.OnPlayerDetach();
        if (initializer)
        {
            GameObject.Destroy(initializer);
            initializer = null;
        }
    }

    public override bool DoFire(bool firstShot)
    {
        if (base.DoFire(firstShot))
        {
            if(ammoValue.type > 0 && player.bag.GetItemCount(ammoValue) < burstRepeat)
            {
                vehicle.entity.PlayOneShot(hornEmptySound);
                return false;
            }
            if(hornCooldown > 0)
            {
                vehicle.entity.PlayOneShot(notReadySound);
                return false;
            }

            if (burstInterval > 0)
                ThreadManager.StartCoroutine(DoHornCo());
            else
            {
                for(int i = 0; i < burstRepeat; ++i)
                    DoHornServer(burstCount);
                vehicle.entity.PlayOneShot(hornReloadSound);
            }
            hornCooldown = hornInterval;

            return true;
        }
        return false;
    }

    protected virtual IEnumerator DoHornCo()
    {
        isCoRunning = true;
        int curBurstCount = 0;
        while(curBurstCount < burstRepeat)
        {
            if (!hasOperator)
                break;
            DoHornServer(burstCount);
            ++curBurstCount;
            yield return new WaitForSeconds(burstInterval);
        }
        yield return null;
        vehicle.entity.PlayOneShot(hornReloadSound);
        isCoRunning = false;
        yield break;
    }

    protected virtual void DoHornServer(int count)
    {
        uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageHornWeaponFire>().Setup(vehicle.entity.entityId, rotator != null ? rotator.HorRotTrans.localEulerAngles.y : 0, rotator != null ? rotator.VerRotTrans.localEulerAngles.x : 0, seat, slot, count, seed));
        else
        {
            if(SingletonMonoBehaviour<ConnectionManager>.Instance.ClientCount() > 0)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageHornWeaponFire>().Setup(vehicle.entity.entityId, rotator != null ? rotator.HorRotTrans.localEulerAngles.y : 0, rotator != null ? rotator.VerRotTrans.localEulerAngles.x : 0, seat, slot, count, seed));
            DoHornClient(count, seed);
        }
        UseHorn();
        if (ammoValue.type > 0)
            ConsumeAmmo(1);
    }

    public virtual void DoHornClient(int count, uint seed)
    {
        if(hornSystem)
        {
            ParticleSystem.EmitParams param = new ParticleSystem.EmitParams();
            param.randomSeed = seed;
            hornSystem.Emit(param, count);
        }
        hornCooldown = hornInterval;
    }

    protected virtual void ConsumeAmmo(int count)
    {
        player.bag.DecItem(ammoValue, count);
    }

    protected virtual void UseHorn()
    {
        vehicle.entity.PlayOneShot(hornFireSound, false);
    }
}
