using System.Collections.Generic;

internal class VPHornWeaponManager : VehiclePart
{
    private List<VPWeaponBase>[] list_weapons;
    private List<VPWeaponRotatorBase>[] list_rotators;
    public static readonly string HornWeaponManagerName = "hornWeaponManager";

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
            }
        }
    }

    public void NetSyncUpdate(int seat, int slot, float horRot, float verRot)
    {
        if (list_rotators[seat] != null)
            list_rotators[seat][slot]?.NetSyncUpdate(horRot, verRot);
    }

    public void DoFire(int seat, bool isHorn)
    {
        if(seat < 0 || seat >= list_weapons.Length || list_weapons[seat] == null)
            return;

        bool firstShot = true;
        if(isHorn)
        {
            foreach(var weapon in list_weapons[seat])
            {
                if(weapon is VPHornWeapon && weapon.DoFire(firstShot))
                    firstShot = false;
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

