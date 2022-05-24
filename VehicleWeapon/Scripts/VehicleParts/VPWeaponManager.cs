using System.Collections.Generic;
using InControl;

public class VPWeaponManager : VehiclePart
{
    private List<VehicleWeaponBase>[] list_weapons;
    public static readonly string VehicleWeaponManagerName = "vehicleWeaponManager";
    private int localPlayerSeat = -1;
    protected EntityPlayerLocal player = null;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        var parts = vehicle.GetParts();
        int seats = 0;
        foreach (var part in parts)
        {
            if (part is VPSeat)
                seats++;
        }
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
                    weapon.SetupWeaponConnections(weapons);
            }
        }
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        /*
        if(localPlayerSeat < 0)
        {
            if (player && player.AttachedToEntity == vehicle.entity)
                OnPlayerEnter();
            else
                return;
        }

        if (vehicle.entity.FindAttachSlot(player) != localPlayerSeat)
        {
            OnPlayerDetach();
            return;
        }
        */

        if(localPlayerSeat >= 0 && !GameManager.Instance.IsPaused())
        {
            if (list_weapons[localPlayerSeat] != null)
            {
                bool GUIOpened = Platform.PlatformManager.NativePlatform.Input.PrimaryPlayer.GUIActions.Enabled;
                if(!GUIOpened)
                    foreach (var weapon in list_weapons[localPlayerSeat])
                        weapon.NoGUIUpdate(_dt);
                foreach (var weapon in list_weapons[localPlayerSeat])
                    weapon.NoPauseUpdate(_dt);

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
        //localPlayerSeat = vehicle.entity.FindAttachSlot(player);
        localPlayerSeat = seat;
        if (list_weapons[localPlayerSeat] != null)
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerEnter();
    }

    internal virtual void OnPlayerDetach()
    {
        PlayerActionsVehicleWeapon.Instance.Enabled = false;
        if (list_weapons[localPlayerSeat] != null)
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerDetach();
        localPlayerSeat = -1;
    }

    public void HandleUserInput()
    {
        int userData = CheckFireState() ? 0 : 1;
        HandleWeaponUserInput(userData);
    }

    protected virtual void HandleWeaponUserInput(int userData)
    {
        foreach (VehicleWeaponBase weapon in list_weapons[localPlayerSeat])
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

