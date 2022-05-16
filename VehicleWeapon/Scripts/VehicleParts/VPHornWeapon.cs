using System;
using System.Collections;
using UnityEngine;

public class VPHornWeapon : VehiclePart
{
    protected int burstCount = 1;
    protected int burstRepeat = 1;
    protected float burstInterval = 0f;
    protected float hornInterval = 1f;
    protected float hornCooldown = 0f;
    protected bool hasOperator = false;
    protected bool explodeOnCollision = true;
    protected bool explodeOnDeath = false;
    protected string hornEmptySound = string.Empty;
    protected string hornNotReadySound = string.Empty;
    protected string hornNotOnTargetSound = string.Empty;
    protected string hornReloadSound = string.Empty;
    protected CustomParticleComponents component = null;
    protected ParticleSystem hornSystem = null;
    protected SubExplosionInitializer initializer = null;
    protected EntityPlayerLocal player = null;
    protected bool isCoRunning = false;
    protected ItemValue ammoValue = ItemValue.None.Clone();
    protected VPHornWeaponRotator rotator = null;
    protected int seat = 0;
    protected int slot = -1;
    protected HornJuncture timing;

    protected enum HornJuncture
    {
        Anytime,
        OnTarget,
        FirstShot,
        FirstShotOnTarget
    }

    public bool HasOperator { get => hasOperator; }
    public ParticleSystem HornSystem { get => hornSystem; }
    public CustomParticleComponents Component { get => component; }
    public VPHornWeaponRotator Rotator { get => rotator; }
    public int Seat { get => seat; }
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
        str = null;
        _properties.ParseString("hornWhen", ref str);
        if(!string.IsNullOrEmpty(str))
            Enum.TryParse<HornJuncture>(str, true, out timing);

        _properties.ParseString("emptySound", ref hornEmptySound);
        _properties.ParseString("notReadySound", ref hornNotReadySound);
        _properties.ParseString("notOnTargetSound", ref hornNotOnTargetSound);
        _properties.ParseString("reloadSound", ref hornReloadSound);

        _properties.ParseInt("seat", ref seat);
        if(seat < 0)
        {
            Log.Error("seat can not be less than 0! setting to 0...");
            seat = 0;
        }

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
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
        string rotatorName = null;
        properties.ParseString("rotator", ref rotatorName);
        if(!string.IsNullOrEmpty(rotatorName))
            rotator = vehicle.FindPart(rotatorName) as VPHornWeaponRotator;

        if (rotator != null)
            rotator.SetHornWeapon(this);
    }

    public void SetSlot(int slot)
    {
        this.slot = slot;
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if (!isCoRunning && hornCooldown > 0)
            hornCooldown -= _dt;

        if (!hasOperator)
        {
            if (player && vehicle.entity.FindAttachSlot(player) == seat)
                OnPlayerEnter();
            else
                return;
        }

        if(vehicle.entity.FindAttachSlot(player) != seat)
        {
            OnPlayerDetach();
            return;
        }
    }

    protected virtual void OnPlayerEnter()
    {
        hasOperator = true;
        initializer = hornSystem.gameObject.AddComponent<SubExplosionInitializer>();
        initializer.data = component.BoundExplosionData;
        initializer.entityAlive = vehicle.entity.AttachedMainEntity as EntityAlive;
        if (component.BoundItemClass != null)
            initializer.value = new ItemValue(component.BoundItemClass.Id);
        if (explodeOnDeath)
            initializer.SetExplodeOnDeath(explodeOnCollision);

        if (rotator != null)
            rotator.CreatePreview();
    }

    protected virtual void OnPlayerDetach()
    {
        hasOperator = false;
        if (initializer)
        {
            GameObject.Destroy(initializer);
            initializer = null;
        }

        if (rotator != null)
            rotator.DestroyPreview();
    }

    public virtual bool DoHorn(bool firstShot)
    {
        switch(timing)
        {
            case HornJuncture.Anytime:
                break;
            case HornJuncture.OnTarget:
                if (rotator != null && !rotator.OnTarget)
                {
                    vehicle.entity.PlayOneShot(hornNotOnTargetSound);
                    return false;
                }
                break;
            case HornJuncture.FirstShot:
                if (!firstShot)
                    return false;
                break;
            case HornJuncture.FirstShotOnTarget:
                if (!firstShot)
                    return false;
                else if (rotator != null && !rotator.OnTarget)
                {
                    vehicle.entity.PlayOneShot(hornNotOnTargetSound);
                    return false;
                }
                break;
        }

        if(ammoValue.type > 0 && player.bag.GetItemCount(ammoValue) < burstRepeat)
        {
            vehicle.entity.PlayOneShot(hornEmptySound);
            return false;
        }
        if(hornCooldown > 0)
        {
            vehicle.entity.PlayOneShot(hornNotReadySound);
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
        string hornSoundName = this.vehicle.GetHornSoundName();
        if (hornSoundName.Length > 0)
        {
            vehicle.entity.PlayOneShot(hornSoundName, false);
        }
    }
}
