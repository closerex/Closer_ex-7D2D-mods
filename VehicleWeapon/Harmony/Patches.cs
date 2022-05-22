using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

/*
[HarmonyPatch(typeof(PlayerMoveController), "Update")]
public class VehicleControlPatch
{

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_usehorn = AccessTools.Method(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn));

        for(int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].Calls(mtd_usehorn))
            {
                codes.RemoveRange(i - 5, 6);
                codes.InsertRange(i - 5, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc, 95),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(PlayerMoveController), "entityPlayerLocal"),
                    CodeInstruction.Call(typeof(VPWeaponManager), nameof(VPWeaponManager.HandleUserInput))
                });
                
                break;
            }
        }

        return codes;
    }
}
*/

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