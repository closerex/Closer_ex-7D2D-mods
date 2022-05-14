using HarmonyLib;

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn))]
class VehicleHornPatch
{
    private static bool Prefix(EntityVehicle __instance)
    {
        VPHornWeapon horn = __instance.GetVehicle().FindPart("hornWeapon") as VPHornWeapon;
        if (horn != null)
        {
            if (__instance.HasDriver && __instance.AttachedMainEntity is EntityPlayerLocal)
                horn.DoHorn();
            return false;
        }
        return true;
    }
}

