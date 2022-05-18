using System.Collections.Generic;

public class VPWeaponManager : VehiclePart
{
    private List<VPWeaponBase>[] list_weapons;
    private List<VPWeaponRotatorBase>[] list_rotators;
    public static readonly string HornWeaponManagerName = "vehicleWeaponManager";

    public static int GetHornWeapon(EntityVehicle entity, EntityPlayerLocal player)
    {
        int seat = entity.FindAttachSlot(player);
        return entity.GetVehicle().FindPart(HornWeaponManagerName) is VPWeaponManager manager && manager.list_weapons[seat] != null ? seat : -1;
    }

    public static void TryUseHorn(EntityVehicle entity, int seat, bool isRelease)
    {
        VPWeaponManager manager = entity.GetVehicle().FindPart(VPWeaponManager.HornWeaponManagerName) as VPWeaponManager;
        manager.DoFire(seat, true, isRelease);
    }

    public override void InitPrefabConnections()
    {
        base.InitPrefabConnections();
        var parts = vehicle.GetParts();
        int seats = 0;
        foreach(var part in parts)
        {
            if(part is VPSeat)
                seats++;
        }    
        list_weapons = new List<VPWeaponBase>[seats];
        list_rotators = new List<VPWeaponRotatorBase>[seats];
        foreach(var part in parts)
        {
            if(part is VPWeaponBase weapon)
            {
                if (list_weapons[weapon.Seat] == null)
                    list_weapons[weapon.Seat] = new List<VPWeaponBase>();
                list_weapons[weapon.Seat].Add(weapon);
            }else if(part is VPWeaponRotatorBase rotator)
            {
                if(list_rotators[rotator.Seat] == null)
                    list_rotators[rotator.Seat] = new List<VPWeaponRotatorBase>();
                list_rotators[rotator.Seat].Add(rotator);
                rotator.SetSlot(list_rotators[rotator.Seat].Count - 1);
            }
        }
        foreach(var weapons in list_weapons)
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

    public void NetSyncUpdate(int seat, int slot, float horRot, float verRot)
    {
        if (list_rotators[seat] != null)
            list_rotators[seat][slot]?.NetSyncUpdate(horRot, verRot);
    }

    public void DoFire(int seat, bool isHorn, bool isRelease)
    {
        if(seat < 0 || seat >= list_weapons.Length || list_weapons[seat] == null)
            return;

        bool firstShot = true;
        if(isHorn)
        {
            foreach(var weapon in list_weapons[seat])
            {
                if(weapon is VPHornWeapon && weapon.DoFire(firstShot, isRelease))
                {
                    firstShot = false;
                    weapon.Fired();
                }
            }
        }
    }

    public void DoHornClient(int seat, int slot, int count, uint seed)
    {
        if (list_weapons[seat] != null)
            (list_weapons[seat][slot] as VPHornWeapon)?.DoHornClient(count, seed);
    }

    public class WeaponSlotComparer : IComparer<VPWeaponBase>
    {
        public int Compare(VPWeaponBase weapon1, VPWeaponBase weapon2)
        {
            return weapon1.Slot - weapon2.Slot;
        }
    }
}

