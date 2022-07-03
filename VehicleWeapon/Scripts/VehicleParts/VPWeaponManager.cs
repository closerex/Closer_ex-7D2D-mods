using System.Collections.Generic;
using System.IO;
using InControl;
using UnityEngine;

public class VPWeaponManager : VehiclePart
{
    private List<VehicleWeaponBase>[] list_weapons;
    public static readonly string VehicleWeaponManagerName = "vehicleWeaponManager";
    private int localPlayerSeat = -1;
    protected EntityPlayerLocal player = null;
    protected Vector3[] cameraOffsets;
    public static Vector3 CameraOffset { get; private set; } = Vector3.zero;

    public override void SetProperties(DynamicProperties _properties)
    {
        base.SetProperties(_properties);

        player = GameManager.Instance.World.GetPrimaryPlayer();
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        var parts = vehicle.GetParts();
        int seats = vehicle.GetTotalSeats();
        list_weapons = new List<VehicleWeaponBase>[seats];
        cameraOffsets = new Vector3[seats];
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
                SortWeapons(weapons);
            
                foreach(var weapon in weapons)
                    weapon.InitWeaponConnections(weapons);
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

                //SortWeapons(weapons);
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
        for (int i = 0; i < cameraOffsets.Length; i++)
        {
            Vector3 cameraOffset = Vector3.up * 2;
            string str = "cameraOffset" + i;
            properties.ParseVec(str, ref cameraOffset);
            cameraOffsets[i] = StringParsers.ParseVector3(vehicleValue.GetVehicleWeaponPropertyOverride(vehicle.GetName() + "_" + tag, str, cameraOffset.ToString()));
        }
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

    public void NetSyncUpdate(int seat, int slot, byte[] updateData)
    {
        if (list_weapons[seat] != null)
        {
            if (updateData != null && updateData.Length > 0)
            {
                using (PooledBinaryReader _br = MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    using (MemoryStream ms = new MemoryStream(updateData))
                    {
                        _br.SetBaseStream(ms);
                        list_weapons[seat][slot].NetSyncRead(_br);
                    }
                }
            }
            else
                list_weapons[seat][slot].NetSyncRead(null);
        }
    }

    public virtual void OnPlayerEnter(int seat)
    {
        PlayerActionsVehicleWeapon.Instance.Enabled = true;
        localPlayerSeat = seat;
        if (list_weapons[localPlayerSeat] != null)
        {
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerEnter();
            CameraOffset = cameraOffsets[localPlayerSeat];
        }
    }

    public virtual void OnPlayerDetach()
    {
        PlayerActionsVehicleWeapon.Instance.Enabled = false;
        if (list_weapons[localPlayerSeat] != null)
            foreach (var weapon in list_weapons[localPlayerSeat])
                weapon.OnPlayerDetach();
        localPlayerSeat = -1;
        CameraOffset = Vector3.zero;
    }

    protected void HandleUserInput()
    {
        int userData = CheckFireState() ? 0 : 1;
        HandleWeaponUserInput(userData);
        HandleCustomInput(userData);
    }

    protected void HandleWeaponUserInput(int userData)
    {
        foreach (VehicleWeaponBase weapon in list_weapons[localPlayerSeat])
            if (weapon.Enabled)
                weapon.HandleUserInput(userData);
    }

    protected virtual void HandleCustomInput(int userData)
    {

    }

    protected bool CheckFireState()
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

    public void DoFireClient(int seat, int slot, int count, byte[] data)
    {
        if (list_weapons[seat] != null)
        {
            if(data != null && data.Length > 0)
            {
                using (PooledBinaryReader _br = MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    _br.SetBaseStream(new MemoryStream(data));
                    list_weapons[seat][slot].DoFireClient(count, _br);
                }
            }else
                list_weapons[seat][slot].DoFireClient(count, null);
        }

    }

    protected void SortWeapons(List<VehicleWeaponBase> weapons)
    {
        weapons.Sort(new WeaponSlotComparer());
        int i = 0;
        foreach (var weapon in weapons)
            weapon.Slot = i++;
    }

    public class WeaponSlotComparer : IComparer<VehicleWeaponBase>
    {
        public int Compare(VehicleWeaponBase weapon1, VehicleWeaponBase weapon2)
        {
            return weapon1.Slot - weapon2.Slot;
        }
    }
}

