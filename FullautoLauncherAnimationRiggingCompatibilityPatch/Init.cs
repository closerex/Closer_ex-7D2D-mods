using FullautoLauncher.Scripts.ProjectileManager;
using HarmonyLib;
using KFCommonUtilityLib.Scripts.Singletons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

public class FLARCompatibilityPatchInit : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out(" Loading Patch: " + GetType());
        var harmony = new HarmonyLib.Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

[HarmonyPatch]
public static class FLARPatch
{
    [HarmonyPatch(typeof(ItemActionBetterLauncher.ItemActionDataBetterLauncher), MethodType.Constructor, new Type[] { typeof(ItemInventoryData), typeof(int) })]
    [HarmonyPostfix]
    private static void Postfix_ctor_ItemActionDataBetterLauncher(ItemActionBetterLauncher.ItemActionDataBetterLauncher __instance, ItemInventoryData _invData)
    {
        __instance.projectileJoint = AnimationRiggingManager.GetTransformOverrideByName("ProjectileJoint", _invData.model);
    }

    [HarmonyPatch(typeof(ProjectileParams), nameof(ProjectileParams.CheckCollision))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_checkCollision_ProjectileMoveScript(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var mtd_block = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageBlock));
        var mtd_entity = AccessTools.Method(typeof(ItemActionAttack), nameof(ItemActionAttack.GetDamageEntity));

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_block))
            {
                codes.InsertRange(i + 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(ProjectileParams), nameof(ProjectileParams.info)),
                    CodeInstruction.LoadField(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueProjectile)),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.GetProjectileBlockDamagePerc)),
                    new CodeInstruction(OpCodes.Mul)
                });
            }
            else if (codes[i].Calls(mtd_entity))
            {
                codes.InsertRange(i + 1, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.LoadField(typeof(ProjectileParams), nameof(ProjectileParams.info)),
                    CodeInstruction.LoadField(typeof(ProjectileParams.ItemInfo), nameof(ProjectileParams.ItemInfo.itemValueProjectile)),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    CodeInstruction.Call(typeof(CommonUtilityPatch), nameof(CommonUtilityPatch.GetProjectileEntityDamagePerc)),
                    new CodeInstruction(OpCodes.Mul)
                });
            }
        }

        return codes;
    }
}