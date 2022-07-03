using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

public class VehicleWeaponBase : VehicleWeaponPartBase
{
    protected int burstCount;
    protected int burstRepeat;
    protected float burstInterval;
    protected float burstDelay;
    protected float repeatInterval;
    protected string emptySound;
    protected string fireSound;
    protected bool fullauto;
    protected ItemValue ammoValue;

    protected bool hasOperator = false;
    protected EntityPlayerLocal player = null;
    protected string notReadySound;
    protected string notOnTargetSound;
    protected string activationSound;
    protected string deactivationSound;
    protected VehicleWeaponRotatorBase rotator = null;
    protected int seat = 0;
    protected int slot = int.MaxValue;
    protected FiringJuncture timing;
    protected bool pressed = false;
    protected bool activated = true;
    protected bool enabled;
    protected Transform enableTrans = null;
    protected MinEffectController effects = new MinEffectController()
    {
        ParentType = MinEffectController.SourceParentType.None,
        EffectGroups = new List<MinEffectGroup>(),
        PassivesIndex = new HashSet<PassiveEffects>(EffectManager.PassiveEffectsComparer)
    };

    VPWeaponManager manager;
    public bool IsCoRunning { get; private set; } = false;

    public event Action<PooledBinaryWriter, VehicleWeaponBase> DynamicUpdateDataCreation
    {
        add => list_update_data_callbacks.Add(value);
        remove => list_update_data_callbacks.Remove(value);
    }

    public event Action<PooledBinaryWriter, VehicleWeaponBase> DynamicFireDataCreation
    {
        add => list_fire_data_callbacks.Add(value);
        remove => list_fire_data_callbacks.Remove(value);
    }

    protected internal List<Action<PooledBinaryWriter, VehicleWeaponBase>> list_update_data_callbacks = new List<Action<PooledBinaryWriter, VehicleWeaponBase>>();
    protected internal List<Action<PooledBinaryWriter, VehicleWeaponBase>> list_fire_data_callbacks = new List<Action<PooledBinaryWriter, VehicleWeaponBase>>();

    protected internal void InvokeUpdateCallbacks(PooledBinaryWriter _bw)
    {
        foreach (var handler in list_update_data_callbacks)
            handler(_bw, this);
    }

    protected internal void InvokeFireCallbacks(PooledBinaryWriter _bw)
    {
        foreach (var handler in list_fire_data_callbacks)
            handler(_bw, this);
    }

    protected enum FiringJuncture
    {
        Anytime,
        OnTarget,
        FirstShot,
        FirstShotOnTarget,
        FromSlotKey,
        FromSlotKeyOnTarget
    }
    public bool HasOperator { get => hasOperator; }
    public bool Activated { get => activated; }
    public bool Enabled { get => enabled; }
    public VehicleWeaponRotatorBase Rotator { get => rotator; }
    public int Seat { get => seat; protected internal set => seat = value; }
    public int Slot { get => slot; set => slot = value; }

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        string str = null;
        _properties.ParseInt("seat", ref seat);
        if (seat < 0)
        {
            Log.Error("seat can not be less than 0! setting to 0...");
            seat = 0;
        }
        _properties.ParseInt("slot", ref slot);

        str = null;
        _properties.ParseString("enableTransform", ref str);
        if (!string.IsNullOrEmpty(str))
            enableTrans = GetTransform(str);

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void ApplyModEffect(ItemValue vehicleValue)
    {
        base.ApplyModEffect(vehicleValue);
        string name = GetModName();
        enabled = true;
        properties.ParseBool("enabled", ref enabled);
        enabled = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "enabled", enabled.ToString()));
        if (enableTrans)
            enableTrans.gameObject.SetActive(enabled);

        properties.ParseString("activationSound", ref activationSound);
        activationSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "activationSound", activationSound);
        properties.ParseString("deactivationSound", ref deactivationSound);
        deactivationSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "deactivationSound", deactivationSound);
        properties.ParseString("notOnTargetSound", ref notOnTargetSound);
        notOnTargetSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "notOnTargetSound", notOnTargetSound);
        properties.ParseString("notReadySound", ref notReadySound);
        notReadySound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "notReadySound", notReadySound);

        string str = null;
        properties.ParseString("fireWhen", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(name, "fireWhen", str);
        timing = FiringJuncture.Anytime;
        if (!string.IsNullOrEmpty(str))
            Enum.TryParse<FiringJuncture>(str, true, out timing);

        burstCount = 1;
        properties.ParseInt("burstCount", ref burstCount);
        burstCount = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstCount", burstCount.ToString()));
        burstInterval = 0f;
        properties.ParseFloat("burstInterval", ref burstInterval);
        burstInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstInterval", burstInterval.ToString()));
        burstRepeat = 1;
        properties.ParseInt("burstRepeat", ref burstRepeat);
        burstRepeat = int.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstRepeat", burstRepeat.ToString()));
        burstDelay = 0f;
        properties.ParseFloat("burstDelay", ref burstDelay);
        burstDelay = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "burstDelay", burstDelay.ToString()));
        repeatInterval = 0f;
        properties.ParseFloat("repeatInterval", ref repeatInterval);
        repeatInterval = float.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "repeatInterval", repeatInterval.ToString()));
        fullauto = false;
        properties.ParseBool("fullauto", ref fullauto);
        fullauto = bool.Parse(vehicleValue.GetVehicleWeaponPropertyOverride(name, "fullauto", fullauto.ToString()));

        str = null;
        ammoValue = ItemValue.None.Clone();
        properties.ParseString("ammo", ref str);
        str = vehicleValue.GetVehicleWeaponPropertyOverride(name, "ammo", str);
        if (!string.IsNullOrEmpty(str))
            ammoValue = ItemClass.GetItem(str, false);
        properties.ParseString("emptySound", ref emptySound);
        emptySound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "emptySound", emptySound);
        properties.ParseString("fireSound", ref fireSound);
        fireSound = vehicleValue.GetVehicleWeaponPropertyOverride(name, "fireSound", fireSound);


        ParseEffectGroups(vehicleValue, this, name);
        if (rotator != null)
            rotator.ApplyModEffect(vehicleValue);
    }

    protected static void ParseEffectGroups(ItemValue vehicleValue, VehicleWeaponBase weapon, string modName)
    {
        weapon.effects.EffectGroups.Clear();
        weapon.effects.PassivesIndex.Clear();
        weapon.effects.ParentPointer = vehicleValue.GetItemId();
        if(vehicleValue.ItemClass != null)
            ParseEffectGroup(vehicleValue.ItemClass.Effects, weapon, modName);

        if(vehicleValue.Modifications != null && vehicleValue.Modifications.Length > 0)
            foreach (var mod in vehicleValue.Modifications)
                if (mod != null && mod.ItemClass is ItemClassModifier)
                    ParseEffectGroup(mod.ItemClass.Effects, weapon, modName);

        if (vehicleValue.CosmeticMods != null && vehicleValue.CosmeticMods.Length > 0)
            foreach (var cos in vehicleValue.CosmeticMods)
                if (cos != null && cos.ItemClass is ItemClassModifier)
                    ParseEffectGroup(cos.ItemClass.Effects, weapon, modName);
    }

    protected static void ParseEffectGroup(MinEffectController effects, VehicleWeaponBase weapon, string modName)
    {
        int i = 0;
        foreach (var effectNodes in effects.EffectGroupXml)
        {
            XmlElement element = effectNodes as XmlElement;
            if (element.HasAttribute("vehicle_weapon") && element.GetAttribute("vehicle_weapon") == modName)
            {
                weapon.effects.EffectGroups.Add(effects.EffectGroups[i]);
                weapon.effects.PassivesIndex.UnionWith(effects.EffectGroups[i].PassivesIndex);
            }
            i++;
        }
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        manager = vehicle.FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;

        string rotatorName = null;
        properties.ParseString("rotator", ref rotatorName);
        if (!string.IsNullOrEmpty(rotatorName))
            rotator = vehicle.FindPart(rotatorName) as VehicleWeaponRotatorBase;

        if (rotator != null)
            rotator.SetWeapon(this);
    }

    public virtual void InitWeaponConnections(IEnumerable<VehicleWeaponBase> weapons)
    {
    }

    public override void NoPauseUpdate(float _dt)
    {
        if (rotator != null)
            rotator.NoPauseUpdate(_dt);

        NetSyncUpdate();
    }

    public override void NoGUIUpdate(float _dt)
    {
        if (rotator != null && GameManager.Instance.GameIsFocused)
            rotator.NoGUIUpdate(_dt);
    }

    protected void NetSyncUpdate(bool forced = false)
    {
        if(forced || ShouldNetSyncUpdate())
        {
            using(PooledExpandableMemoryStream ms = MemoryPools.poolMemoryStream.AllocSync(true))
            {
                using(PooledBinaryWriter _bw = MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    _bw.SetBaseStream(ms);
                    InvokeUpdateCallbacks(_bw);
                    NetSyncWrite(_bw);
                    byte[] updateData = ms.ToArray();
                    if (ConnectionManager.Instance.IsServer && ConnectionManager.Instance.ClientCount() > 0)
                        ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageVehicleWeaponUpdate>().Setup(vehicle.entity.entityId, seat, slot, updateData), false, -1, player.entityId, vehicle.entity.entityId, 75);
                    else if (ConnectionManager.Instance.IsClient)
                        ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageVehicleWeaponUpdate>().Setup(vehicle.entity.entityId, seat, slot, updateData));
                }
            }

        }
    }

    public override bool ShouldNetSyncUpdate()
    {
        return rotator != null && rotator.ShouldNetSyncUpdate() && ((ConnectionManager.Instance.IsServer && ConnectionManager.Instance.ClientCount() > 0 ) || ConnectionManager.Instance.IsClient);
    }

    public override void NetSyncRead(PooledBinaryReader _br)
    {
        if (rotator != null && _br != null)
            rotator.NetSyncRead(_br);
    }

    public override void NetSyncWrite(PooledBinaryWriter _bw)
    {
        if(rotator != null)
            rotator.NetSyncWrite(_bw);
    }

    public virtual void OnPlayerEnter()
    {
        hasOperator = true;

        if (activated)
            OnActivated();
    }

    public virtual void OnPlayerDetach()
    {
        hasOperator = false;
        pressed = false;

        OnDeactivated();
    }

    protected internal virtual void OnActivated()
    {
        if (rotator != null)
            rotator.CreatePreview();
    }

    protected internal virtual void OnDeactivated()
    {
        if (rotator != null)
            rotator.DestroyPreview();
    }

    protected void ToggleActivated()
    {
        activated = !activated;
        if (activated)
        {
            OnActivated();
            vehicle.entity.PlayOneShot(activationSound);
        }
        else
        {
            OnDeactivated();
            vehicle.entity.PlayOneShot(deactivationSound);
        }
    }

    public virtual void HandleUserInput(int userData)
    {
        var vw_input = PlayerActionsVehicleWeapon.Instance;
        if (slot < vw_input.ActivateActions.Count && (vw_input.ActivateActions[slot].IsPressed || vw_input.ActivateActions[slot].WasReleased))
        {
            if (vw_input.HoldToggleActivated.IsPressed)
            {
                if(vw_input.ActivateActions[slot].WasPressed)
                    ToggleActivated();
            }
            else
            {
                bool firstShot = (userData & 1) > 0;
                DoFireLocal(ref firstShot, vw_input.ActivateActions[slot].WasReleased, true);
            }
        }
    }

    public void DoFireLocal(ref bool firstShot, bool isRelease, bool fromSlot = false)
    {
        if(CanFire(firstShot, isRelease, fromSlot))
        {
            firstShot = false;
            DoFire();
        }
    }

    protected internal virtual bool CanFire(bool firstShot, bool isRelease, bool fromSlot)
    {
        if (!activated || !enabled)
            return false;

        switch (timing)
        {
            case FiringJuncture.Anytime:
                break;
            case FiringJuncture.OnTarget:
                if (rotator != null && !rotator.OnTarget)
                {
                    if(!pressed)
                        vehicle.entity.PlayOneShot(notOnTargetSound);
                    pressed = true;
                    return false;
                }
                break;
            case FiringJuncture.FirstShot:
                if (!firstShot)
                    return false;
                break;
            case FiringJuncture.FirstShotOnTarget:
                if (!firstShot)
                    return false;
                else if (rotator != null && !rotator.OnTarget)
                    goto notOnTarget;
                break;
            case FiringJuncture.FromSlotKey:
                if (!fromSlot)
                    return false;
                break;
            case FiringJuncture.FromSlotKeyOnTarget:
                if (!fromSlot)
                    return false;
                else if (rotator != null && !rotator.OnTarget)
                    goto notOnTarget;
                break;
        }
        return true;
    notOnTarget:
        if (!pressed)
            vehicle.entity.PlayOneShot(notOnTargetSound);
        pressed = true;
        return false;
    }

    protected internal virtual void DoFire()
    {
        if (burstInterval > 0 || burstDelay > 0)
            ThreadManager.StartCoroutine(DoFireCo());
        else
        {
            DoFireNow();
            OnFireFinished();
        }
    }

    protected virtual IEnumerator DoFireCo()
    {
        IsCoRunning = true;
        if (burstDelay > 0)
            yield return new WaitForSecondsRealtime(burstDelay);
        if (burstInterval > 0)
        {
            int curBurstCount = 0;
            while (curBurstCount < burstRepeat)
            {
                if (!hasOperator)
                    break;
                DoFireServer(burstCount);
                ++curBurstCount;
                yield return new WaitForSecondsRealtime(burstInterval);
            }
            if (repeatInterval > 0)
                yield return new WaitForSecondsRealtime(repeatInterval);
        }
        else
            DoFireNow();

        IsCoRunning = false;
        OnFireFinished();
        yield break;
    }

    protected virtual void DoFireNow()
    {
        for (int i = 0; i < burstRepeat; ++i)
            DoFireServer(burstCount);
    }

    protected virtual void OnFireFinished()
    {
    }

    protected void DoFireServer(int count)
    {
        byte[] updateData = null;
        byte[] fireData = null;
        using(PooledBinaryWriter _bw = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            using(PooledExpandableMemoryStream ms = MemoryPools.poolMemoryStream.AllocSync(true))
            {
                _bw.SetBaseStream(ms);
                InvokeUpdateCallbacks(_bw);
                NetSyncWrite(_bw);
                updateData = ms.ToArray();
            }

            using(PooledExpandableMemoryStream ms = MemoryPools.poolMemoryStream.AllocSync(true))
            {
                _bw.SetBaseStream(ms);
                _bw.Seek(0, SeekOrigin.Begin);
                InvokeFireCallbacks(_bw);
                NetFireWrite(_bw);
                fireData = ms.ToArray();
            }
        }

        if (!ConnectionManager.Instance.IsServer)
            ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageVehicleWeaponFire>().Setup(vehicle.entity.entityId, seat, slot, updateData, count, fireData));
        else
        {
            if (ConnectionManager.Instance.ClientCount() > 0)
                ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageVehicleWeaponFire>().Setup(vehicle.entity.entityId, seat, slot, updateData, count, fireData));
            manager.DoFireClient(seat, slot, count, fireData);
        }
        if (ammoValue.type > 0)
            ConsumeAmmo(1);
    }

    protected virtual void NetFireWrite(PooledBinaryWriter _bw)
    {
    }

    public virtual void DoFireClient(int count, PooledBinaryReader _br)
    {
        if (hasOperator)
            Fired();

        Audio.Manager.Play(vehicle.entity, fireSound);
    }

    protected virtual void ConsumeAmmo(int count)
    {
        player.bag.DecItem(ammoValue, count);
    }

    protected internal virtual void Fired()
    {
        player.MinEventContext.ItemValue = ItemValue.None.Clone();
        player.FireEvent(MinEventTypes.onSelfRangedBurstShot, false);
        player.MinEventContext.ItemValue = vehicle.GetUpdatedItemValue();
        effects.FireEvent(MinEventTypes.onSelfRangedBurstShot, player.MinEventContext);
    }
}

