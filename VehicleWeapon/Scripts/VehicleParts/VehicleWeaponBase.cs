using System;
using System.Collections.Generic;
using UnityEngine;

public class VehicleWeaponBase : VehicleWeaponPartBase
{
    protected bool hasOperator = false;
    protected EntityPlayerLocal player = null;
    protected string notReadySound = string.Empty;
    protected string notOnTargetSound = string.Empty;
    protected string activationSound = string.Empty;
    protected string deactivationSound = string.Empty;
    protected VehicleWeaponRotatorBase rotator = null;
    protected int seat = 0;
    protected int slot = int.MaxValue;
    protected FiringJuncture timing;
    protected bool pressed = false;
    protected VehicleWeaponBase cycleNext = null;
    protected float cycleInterval = 0f;
    protected float cycleCooldown = 0f;
    protected bool activated = true;

    protected enum FiringJuncture
    {
        Anytime,
        OnTarget,
        FirstShot,
        FirstShotOnTarget,
        Cycle,
        FromSlotKey,
        FromSlotKeyOnTarget
    }
    public bool HasOperator { get => hasOperator; }
    public bool Activated { get => activated; }
    public VehicleWeaponRotatorBase Rotator { get => rotator; }
    public int Seat { get => seat; }
    public int Slot { get => slot; set => slot = value; }
    public bool IsCurCycle { get; set; } = false;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        _properties.ParseString("notReadySound", ref notReadySound);
        _properties.ParseString("notOnTargetSound", ref notOnTargetSound);
        _properties.ParseString("activationSound", ref activationSound);
        _properties.ParseString("deactivationSound", ref deactivationSound);
        string str = null;
        _properties.ParseString("fireWhen", ref str);
        if (!string.IsNullOrEmpty(str))
            Enum.TryParse<FiringJuncture>(str, true, out timing);

        _properties.ParseInt("seat", ref seat);
        if (seat < 0)
        {
            Log.Error("seat can not be less than 0! setting to 0...");
            seat = 0;
        }
        _properties.ParseInt("slot", ref slot);

        player = GameManager.Instance.World.GetPrimaryPlayer();
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

    public virtual void SetupWeaponConnections(in List<VehicleWeaponBase> weapons)
    {
        if (timing != FiringJuncture.Cycle)
            return;

        int nextIndex = -1;
        properties.ParseInt("cycleNextSlot", ref nextIndex);
        if(nextIndex < 0 || nextIndex >= weapons.Count)
        {
            Log.Error("cycle index out of range!");
            return;
        }
        cycleNext = weapons[nextIndex];
        properties.ParseFloat("cycleInterval", ref cycleInterval);
        if (nextIndex < slot)
            cycleNext.IsCurCycle = true;
    }

    public virtual void SetCurCycle()
    {
        IsCurCycle = true;
        cycleCooldown = cycleInterval;
    }

    public override void NoPauseUpdate(float _dt)
    {
        if (IsCurCycle && cycleCooldown > 0)
            cycleCooldown -= _dt;

        if (rotator != null)
            rotator.NoPauseUpdate(_dt);
    }

    public override void NoGUIUpdate(float _dt)
    {
        if (rotator != null)
            rotator.NoGUIUpdate(_dt);
    }

    public virtual void NetSyncUpdate(float horRot, float verRot)
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

    protected virtual void OnActivated()
    {
        if (rotator != null)
            rotator.CreatePreview();
    }

    protected virtual void OnDeactivated()
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
        if(DoFire(firstShot, isRelease, fromSlot))
        {
            firstShot = false;
            Fired();
        }
    }

    protected virtual bool DoFire(bool firstShot, bool isRelease, bool fromSlot)
    {
        if (!activated)
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
            case FiringJuncture.Cycle:
                if (cycleNext == null || !IsCurCycle || cycleCooldown > 0)
                    return false;
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

    protected virtual void Fired()
    {
        if (timing == FiringJuncture.Cycle)
        {
            IsCurCycle = false;
            cycleNext.SetCurCycle();
        }
    }
}

