using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

[HarmonyPatch(typeof(EntityVehicle))]
class VehicleManagerPatch
{
    [HarmonyPatch(nameof(EntityVehicle.Kill))]
    [HarmonyPostfix]
    private static void Postfix_Kill(EntityVehicle __instance)
    {
        var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
        if (manager != null)
            manager.Cleanup();
    }

    [HarmonyPatch(nameof(EntityVehicle.AttachEntityToSelf))]
    [HarmonyPostfix]
    private static void Postfix_AttachEntityToSelf(Entity _entity, int __result, EntityVehicle __instance)
    {
        if(__result >= 0 && _entity is EntityPlayerLocal)
        {
            var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
            if (manager != null)
                manager.OnPlayerEnter(__result);
        }
    }

    [HarmonyPatch("DetachEntity")]
    [HarmonyPostfix]
    private static void Postfix_DetachEntity(Entity _entity, EntityVehicle __instance)
    {
        if (_entity is EntityPlayerLocal)
        {
            var manager = __instance.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
            if (manager != null)
                manager.OnPlayerDetach();
        }
    }
}

[HarmonyPatch(typeof(VehicleManager), nameof(VehicleManager.RemoveAllVehiclesFromMap))]
public class VehicleCleanupPatch
{
    private static bool Prefix(VehicleManager __instance, List<EntityVehicle> ___vehiclesActive)
    {
        foreach(var entity in ___vehiclesActive)
        {
            var manager = entity.GetVehicle().FindPart(VPWeaponManager.VehicleWeaponManagerName) as VPWeaponManager;
            if (manager != null)
                manager.Cleanup();
        }

        return true;
    }
}