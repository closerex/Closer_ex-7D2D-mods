﻿using HarmonyLib;

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn))]
class VehicleHornPatch
{
    private static bool Prefix(EntityVehicle __instance)
    {
        VPHornWeaponManager horn = __instance.GetVehicle().FindPart(VPHornWeaponManager.HornWeaponManagerName) as VPHornWeaponManager;
        if (horn != null)
        {
            if (!GameManager.IsDedicatedServer)
                horn.DoHorn(__instance.FindAttachSlot(__instance.world.GetPrimaryPlayer()));
            return false;
        }
        return true;
    }
}
