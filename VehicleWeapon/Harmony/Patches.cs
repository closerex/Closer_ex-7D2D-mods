using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using InControl;

/*
[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn))]
class VehicleHornPatch
{
    private static bool Prefix(EntityVehicle __instance)
    {
        VPWeaponManager horn = __instance.GetVehicle().FindPart(VPWeaponManager.HornWeaponManagerName) as VPWeaponManager;
        if (horn != null)
        {
            if (!GameManager.IsDedicatedServer)
                horn.DoFire(__instance.FindAttachSlot(__instance.world.GetPrimaryPlayer()), true);
            return false;
        }
        return true;
    }
}
*/

[HarmonyPatch(typeof(PlayerMoveController), "Update")]
public class VehicleControlPatch
{
    public static void CheckHornState(PlayerAction action, EntityVehicle entity, EntityPlayerLocal player)
    {
        if (action.IsPressed || action.WasReleased)
        {
            int seat = VPWeaponManager.GetHornWeapon(entity, player);
            if (seat >= 0)
                VPWeaponManager.TryUseHorn(entity, seat, action.WasReleased);
            else if (seat < 0 && action.WasPressed)
                entity.UseHorn();
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mtd_usehorn = AccessTools.Method(typeof(EntityVehicle), nameof(EntityVehicle.UseHorn));

        for(int i = 0; i < codes.Count; ++i)
        {
            if (codes[i].Calls(mtd_usehorn))
            {
                codes.RemoveRange(i - 3, 4);
                codes.InsertRange(i - 3, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc, 95),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(PlayerMoveController), "entityPlayerLocal"),
                    CodeInstruction.Call(typeof(VehicleControlPatch), nameof(VehicleControlPatch.CheckHornState), new System.Type[] { typeof(InControl.PlayerAction), typeof(EntityVehicle), typeof(EntityPlayerLocal) })
                });
                
                break;
            }
        }

        return codes;
    }
}