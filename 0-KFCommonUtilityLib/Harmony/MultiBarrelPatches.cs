using HarmonyLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace KFCommonUtilityLib.Harmony
{
    [HarmonyPatch]
    public class MultiBarrelPatches
    {
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.ExecuteAction))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemActionRanged(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();

            var mtd_getmax = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.GetMaxAmmoCount));
            var mtd_consume = AccessTools.Method(typeof(ItemActionRanged), nameof(ItemActionRanged.ConsumeAmmo));

            Label loopStart = generator.DefineLabel();
            Label loopCondi = generator.DefineLabel();
            LocalBuilder lbd_data_module = generator.DeclareLocal(typeof(ActionModuleMultiBarrel.MultiBarrelData));
            LocalBuilder lbd_ismb = generator.DeclareLocal(typeof(bool));
            LocalBuilder lbd_i = generator.DeclareLocal(typeof(int));
            LocalBuilder lbd_rounds = generator.DeclareLocal(typeof(int));
            for (int i = 0; i < codes.Count; i++)
            {
                //prepare loop and store local variables
                if (codes[i].opcode == OpCodes.Stloc_S && ((LocalBuilder)codes[i].operand).LocalIndex == 6)
                {
                    codes[i + 1].WithLabels(loopStart);
                    Label lbl = generator.DefineLabel();
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldnull),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_data_module),
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldloca_S, lbd_data_module),
                        CodeInstruction.Call(typeof(MultiBarrelPatches), nameof(IsMultiBarrelData)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_ismb),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_i),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_ismb),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                        CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.roundsPerShot)),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_rounds),
                        new CodeInstruction(OpCodes.Br_S, loopCondi),
                        new CodeInstruction(OpCodes.Ldc_I4_1).WithLabels(lbl),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_rounds),
                        new CodeInstruction(OpCodes.Br_S, loopCondi),
                    });
                    i += 16;
                }
                //one round multi shot check
                else if (codes[i].Calls(mtd_consume))
                {
                    Label lbl = generator.DefineLabel();
                    codes[i - 2].WithLabels(lbl);
                    codes.InsertRange(i - 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_ismb),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                        CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.oneRoundMultishot)),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_i),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Bgt_S, codes[i - 3].operand)
                    });
                    i += 8;
                }
                //loop conditions and cycle barrels
                else if (codes[i].Calls(mtd_getmax))
                {
                    Label lbl_pre = generator.DefineLabel();
                    Label lbl_post = generator.DefineLabel();
                    CodeInstruction origin = codes[i - 2];
                    codes.InsertRange(i - 2, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_ismb).WithLabels(origin.ExtractLabels()),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl_pre),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                        CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.muzzleIsPerRound)),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl_pre),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                        CodeInstruction.Call(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.CycleBarrels)),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_i).WithLabels(lbl_pre),
                        //new CodeInstruction(OpCodes.Dup),
                        //new CodeInstruction(OpCodes.Ldloc_S, lbd_rounds),
                        //CodeInstruction.Call(typeof(MultiBarrelPatches), nameof(MultiBarrelPatches.LogInfo)),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Stloc_S, lbd_i),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_i).WithLabels(loopCondi),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_rounds),
                        new CodeInstruction(OpCodes.Blt_S, loopStart),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_ismb),
                        new CodeInstruction(OpCodes.Brfalse_S, lbl_post),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                        CodeInstruction.LoadField(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.muzzleIsPerRound)),
                        new CodeInstruction(OpCodes.Brtrue_S, lbl_post),
                        new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                        CodeInstruction.Call(typeof(ActionModuleMultiBarrel.MultiBarrelData), nameof(ActionModuleMultiBarrel.MultiBarrelData.CycleBarrels))
                    });
                    origin.WithLabels(lbl_post);
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateEnter))]
        [HarmonyPostfix]
        private static void Postfix_OnStateEnter_AnimatorRangedReloadState(AnimatorRangedReloadState __instance)
        {
            ItemActionLauncher.ItemActionDataLauncher launcherData = __instance.actionData as ItemActionLauncher.ItemActionDataLauncher;
            if (launcherData != null && launcherData is IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData> dataModule && dataModule.Instance.oneRoundMultishot && dataModule.Instance.roundsPerShot > 1)
            {
                int count = launcherData.projectileInstance.Count;
                int times = dataModule.Instance.roundsPerShot - 1;
                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < times; j++)
                    {
                        launcherData.projectileJoint = dataModule.Instance.projectileJoints[j + 1];
                        launcherData.projectileInstance.Insert(i * (times + 1) + j + 1,((ItemActionLauncher)__instance.actionRanged).instantiateProjectile(launcherData));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AnimatorRangedReloadState), nameof(AnimatorRangedReloadState.OnStateExit))]
        [HarmonyPostfix]
        private static void Postfix_OnStateExit_AnimatorRangedReloadState(AnimatorRangedReloadState __instance)
        {
            if (__instance.actionData is IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData> dataModule)
            {
                dataModule.Instance.SetCurrentBarrel(__instance.actionData.invData.itemValue.Meta);
            }
        }

        private static bool IsMultiBarrelData(ItemActionData data, out ActionModuleMultiBarrel.MultiBarrelData dataModule)
        {
            return (dataModule = (data as IModuleContainerFor<ActionModuleMultiBarrel.MultiBarrelData>)?.Instance) != null;
        }

        private static void LogInfo(int cur, int max) => Log.Out($"max rounds {max}, cur {cur}");
    }
}
