using System.Collections.Generic;
using InControl;
using UnityEngine;

public class VPWeaponManager : VehiclePart
{
    private List<VehicleWeaponBase>[] list_weapons;
    public static readonly string VehicleWeaponManagerName = "vehicleWeaponManager";
    private int localPlayerSeat = -1;
    protected EntityPlayerLocal player = null;
    protected Vector3 cameraOffset = Vector3.up * 2;
    public static Vector3 CameraOffset { get; private set; } = Vector3.zero;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        _properties.ParseVec("cameraOffset", ref cameraOffset);
        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        var parts = vehicle.GetParts();
        int seats = vehicle.entity.GetAttachMaxCount();
        list_weapons = new List<VehicleWeaponBase>[seats];
        foreach (var part in parts)
        {
            if (part is VehicleWeaponBase weapon)
            {
                if (list_weapons[weapon.Seat] == null)
                    list_weapons[weapon.Seat] = new List<VehicleWeaponBase>();
                list_weapons[weapon.Seat].Add(weapon);
            }
        }
        foreach (var weapons in list_weapons)
        {
            if (weapons != null)
            {
                weapons.Sort(new WeaponSlotComparer());
                int i = 0;
                foreach (var weapon in weapons)
                    weapon.Slot = i++;

                foreach (var weapon in weapons)
                {
                    weapon.ApplyModEffect(vehicle.GetUpdatedItemValue());
                    weapon.SetupWeaponConnections(weapons);
                }
            }
        }
    }

    public virtual void ApplyModEffect(ItemValue vehicleValue)
    {
        ParseModProperties(vehicleValue);

        foreach (var weapons in list_weapons)
        {
            if (weapons != null)
            {
                foreach (var weapon in weapons)
                    weapon.ApplyModEffect(vehicleValue);
            }
        }

        if(localPlayerSeat >= 0)
        {
            int curSeat = localPlayerSeat;
            OnPlayerDetach();
            OnPlayerEnter(curSeat);
        }
    }

    protected virtual void ParseModProperties(ItemValue vehicleValue)
    {
        cameraOffset = StringParsers.ParseVector3(vehicleValue.GetVehicleWeaponPropertyOverride(vehicle.GetName() + "_" + tag, "cameraOffset", cameraOffset.ToString()));
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        if(localPlayerSeat >= 0)
        {
            if (list_weapons[localPlayerSeat] != null && !GameManager.Instance.IsPaused())
            {
                bool GUIOpened = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.GUIActions.Enabled;
                foreach (var weapon in list_weapons[localPlayerSeat])
                {
                    if (!weapon.Enabled || !weapon.Activated)
                        continue;

                    if(!GUIOpened)
                        weapon.NoGUIUpdate(_dt);
                    weapon.NoPauseUpdate(_dt);
                }

                if(!GUIOpened)
                    HandleUserInput();
            }
        }
    }

    public void NetSyncUpdate(int seat, int slot, float horRot, float verRot)
    {
        if (list_weapons[seat] != null)
            list_weapons[seat][slot].NetSyncUpdate(horRot, verRot);
    }

    internal virtual void OnPlayerEnter(int seat)
    {
        PlayerActionsVehicleWeapon.Instance.Enabled = true;
        localPlayerSeat = seat;
        if (list_weapons[localPlayerSeat] != null)
        {
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerEnter();
            CameraOffset = cameraOffset;
        }
    }

    internal virtual void OnPlayerDetach()
    {
        PlayerActionsVehicleWeapon.Instance.Enabled = false;
        if (list_weapons[localPlayerSeat] != null)
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerDetach();
        localPlayerSeat = -1;
        CameraOffset = Vector3.zero;
    }

    public void HandleUserInput()
    {
        int userData = CheckFireState() ? 0 : 1;
        HandleWeaponUserInput(userData);
    }

    protected virtual void HandleWeaponUserInput(int userData)
    {
        foreach (VehicleWeaponBase weapon in list_weapons[localPlayerSeat])
            if (weapon.Enabled)
                weapon.HandleUserInput(userData);
    }

    protected virtual bool CheckFireState()
    {
        var fireState = PlayerActionsVehicleWeapon.Instance.FireShot;
        if (fireState.IsPressed || fireState.WasReleased)
            return DoFireLocal(fireState.WasReleased);
        return false;
    }

    public virtual void Cleanup()
    {
        if (localPlayerSeat >= 0)
            OnPlayerDetach();
    }

    public bool DoFireLocal(bool isRelease)
    {
        if(localPlayerSeat < 0 || list_weapons[localPlayerSeat] == null)
            return false;

        bool firstShot = true;
        foreach(var weapon in list_weapons[localPlayerSeat])
            weapon.DoFireLocal(ref firstShot, isRelease);
        return firstShot;
    }

    public void DoParticleFireClient(int seat, int slot, int count, uint seed)
    {
        if (list_weapons[seat] != null)
            (list_weapons[seat][slot] as VPParticleWeapon)?.DoParticleFireClient(count, seed);
    }

    public class WeaponSlotComparer : IComparer<VehicleWeaponBase>
    {
        public int Compare(VehicleWeaponBase weapon1, VehicleWeaponBase weapon2)
        {
            return weapon1.Slot - weapon2.Slot;
        }
    }
}

