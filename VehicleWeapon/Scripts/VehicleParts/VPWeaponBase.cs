using System;
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
    protected HornJuncture timing;

    protected enum HornJuncture
    {
        Anytime,
        OnTarget,
        FirstShot,
        FirstShotOnTarget
    }
    public bool HasOperator { get => hasOperator; }
    public VPWeaponRotatorBase Rotator { get => rotator; }
    public int Seat { get => seat; }
    public int Slot { get => slot; set => slot = value; }

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);
        _properties.ParseString("notReadySound", ref notReadySound);
        _properties.ParseString("notOnTargetSound", ref notOnTargetSound);
        string str = null;
        _properties.ParseString("hornWhen", ref str);
        if (!string.IsNullOrEmpty(str))
            Enum.TryParse<HornJuncture>(str, true, out timing);

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

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if (!hasOperator)
        {
            if (player && vehicle.entity.FindAttachSlot(player) == seat)
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

        if (rotator != null)
            rotator.DestroyPreview();
    }

    public virtual bool DoFire(bool firstShot)
    {
        switch (timing)
        {
            case HornJuncture.Anytime:
                break;
            case HornJuncture.OnTarget:
                if (rotator != null && !rotator.OnTarget)
                {
                    vehicle.entity.PlayOneShot(notOnTargetSound);
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
                    vehicle.entity.PlayOneShot(notOnTargetSound);
                    return false;
                }
                break;
        }
        return true;
    }
}

