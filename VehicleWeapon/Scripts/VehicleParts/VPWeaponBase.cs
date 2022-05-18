using System;
using System.Collections.Generic;
using UnityEngine;

public class VPWeaponBase : VehiclePart
{
    protected bool hasOperator = false;
    protected EntityPlayerLocal player = null;
    protected string notReadySound = string.Empty;
    protected string notOnTargetSound = string.Empty;
    protected VPWeaponRotatorBase rotator = null;
    protected int seat = 0;
    protected int slot = int.MaxValue;
    protected FiringJuncture timing;
    protected bool pressed = false;
    protected VPWeaponBase cycleNext = null;
    protected float cycleInterval = 0f;
    protected float cycleCooldown = 0f;

    protected enum FiringJuncture
    {
        Anytime,
        OnTarget,
        FirstShot,
        FirstShotOnTarget,
        Cycle
    }
    public bool HasOperator { get => hasOperator; }
    public VPWeaponRotatorBase Rotator { get => rotator; }
    public int Seat { get => seat; }
    public int Slot { get => slot; set => slot = value; }
    public bool IsCurCycle { get; set; } = false;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        _properties.ParseString("notReadySound", ref notReadySound);
        _properties.ParseString("notOnTargetSound", ref notOnTargetSound);
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
            rotator = vehicle.FindPart(rotatorName) as VPWeaponRotatorBase;

        if (rotator != null)
            rotator.SetWeapon(this);
    }

    public virtual void SetupWeaponConnections(in List<VPWeaponBase> weapons)
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
        Log.Out("cycle next: " + nextIndex + " is cur: " + IsCurCycle);
    }

    public virtual void SetCurCycle()
    {
        IsCurCycle = true;
        cycleCooldown = cycleInterval;
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if (IsCurCycle && cycleCooldown > 0)
            cycleCooldown -= _dt;

        if (!hasOperator)
        {
            if (player && player.AttachedToEntity == vehicle.entity && vehicle.entity.FindAttachSlot(player) == seat)
                OnPlayerEnter();
            else
                return;
        }

        if (vehicle.entity.FindAttachSlot(player) != seat)
        {
            OnPlayerDetach();
            return;
        }
    }

    protected virtual void OnPlayerEnter()
    {
        hasOperator = true;

        if (rotator != null)
            rotator.CreatePreview();
    }

    protected virtual void OnPlayerDetach()
    {
        hasOperator = false;
        pressed = false;

        if (rotator != null)
            rotator.DestroyPreview();
    }

    public virtual bool DoFire(bool firstShot, bool isRelease)
    {
        switch (timing)
        {
            case FiringJuncture.Anytime:
                break;
            case FiringJuncture.OnTarget:
                if (rotator != null && !rotator.OnTarget)
                {
                    vehicle.entity.PlayOneShot(notOnTargetSound);
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
                {
                    vehicle.entity.PlayOneShot(notOnTargetSound);
                    return false;
                }
                break;
            case FiringJuncture.Cycle:
                if (cycleNext == null || !IsCurCycle || cycleCooldown > 0)
                    return false;
                break;
        }
        return true;
    }

    public virtual void Fired()
    {
        if (timing == FiringJuncture.Cycle)
        {
            IsCurCycle = false;
            cycleNext.SetCurCycle();
        }
    }
}

