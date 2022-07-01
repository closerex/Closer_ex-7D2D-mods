using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public class VehicleWeaponBase : VehicleWeaponPartBase
{
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
    public List<int> UserData { get; } = new List<int>();

    public virtual void AddUserData(int data)
    {
        UserData.Add(data);
    }

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
    }

    public override void NoGUIUpdate(float _dt)
    {
        if (rotator != null && GameManager.Instance.GameIsFocused)
            rotator.NoGUIUpdate(_dt);
    }

    public virtual void NetSyncUpdate(float horRot, float verRot, Stack<int> userData)
    {
        if (rotator != null)
            rotator.NetSyncUpdate(horRot, verRot);
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
            Fired();
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
    }

    protected internal virtual void Fired()
    {
        player.MinEventContext.ItemValue = ItemValue.None.Clone();
        player.FireEvent(MinEventTypes.onSelfRangedBurstShot, false);
        effects.FireEvent(MinEventTypes.onSelfRangedBurstShot, player.MinEventContext);
    }
}

