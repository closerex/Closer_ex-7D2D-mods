using System.Collections.Generic;

internal class VPHornWeaponManager : VehiclePart
{
    private List<VPHornWeapon>[] list_weapons;
    private List<VPHornWeaponRotator>[] list_rotators;
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
        list_weapons = new List<VPHornWeapon>[seats];
        list_rotators = new List<VPHornWeaponRotator>[seats];
        foreach(var part in parts)
        {
            if(part is VPHornWeapon weapon)
            {
                if (list_weapons[weapon.Seat] == null)
                    list_weapons[weapon.Seat] = new List<VPHornWeapon>();
                list_weapons[weapon.Seat].Add(weapon);
                weapon.SetSlot(list_weapons[weapon.Seat].Count - 1);
            }else if(part is VPHornWeaponRotator rotator)
            {
                if(list_rotators[rotator.Seat] == null)
                    list_rotators[rotator.Seat] = new List<VPHornWeaponRotator>();
                list_rotators[rotator.Seat].Add(rotator);
                rotator.SetSlot(list_rotators[rotator.Seat].Count - 1);
            }
        }
    }

    public void NetSyncUpdate(int seat, int slot, float horRot, float verRot)
    {
        if (list_rotators[seat] != null)
            list_rotators[seat][slot]?.NetSyncUpdate(horRot, verRot);
    }

    public void DoHorn(int seat)
    {
        if(seat < 0 || seat >= list_weapons.Length || list_weapons[seat] == null)
            return;

        bool firstShot = true;
        foreach(var weapon in list_weapons[seat])
            firstShot = !weapon.DoHorn(firstShot);
    }

    public void DoHornClient(int seat, int slot, int count, uint seed)
    {
        if (list_weapons[seat] != null)
            list_weapons[seat][slot]?.DoHornClient(count, seed);
    }
}

