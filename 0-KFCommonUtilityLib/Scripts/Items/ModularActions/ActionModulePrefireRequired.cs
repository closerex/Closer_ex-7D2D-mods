using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionDynamicMelee)), TypeDataTarget(typeof(PrefireRequiredData))]
public class ActionModulePrefireRequired
{
    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var lbd_data_module = generator.DeclareLocal(typeof(PrefireRequiredData));
        var lbd_prefire = generator.DeclareLocal(typeof(bool));
        var lbl_prefire_end = generator.DefineLabel();

        var fld_weaponheadpos = AccessTools.Field(typeof(ItemActionDynamic.ItemActionDynamicData), nameof(ItemActionDynamic.ItemActionDynamicData.lastWeaponHeadPosition));

        bool firstReleasePatched = false;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldarg_2 && !firstReleasePatched)
            {
                var lbl = generator.DefineLabel();
                var lbl_release = codes[i + 1].operand;
                codes[i + 2].WithLabels(lbl);
                codes[i + 1].opcode = OpCodes.Brfalse_S;
                codes[i + 1].operand = lbl;
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc, lbd_prefire),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl_release),
                    new CodeInstruction(OpCodes.Br_S, lbl_prefire_end)
                });
                i += 5;
                firstReleasePatched = true;
            }
            else if (codes[i].StoresField(fld_weaponheadpos))
            {
                codes.InsertRange(i - 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    CodeInstruction.LoadField(typeof(ItemActionData), nameof(ItemActionData.lastUseTime)),
                    CodeInstruction.StoreField(typeof(PrefireRequiredData), nameof(PrefireRequiredData.prefireStartTime))
                });
                i += 4;
            }

        }

        var lbl_vanilla = generator.DefineLabel();
        codes[0].WithLabels(lbl_vanilla);
        codes.InsertRange(0, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Castclass, typeof(IModuleContainerFor<PrefireRequiredData>)),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<PrefireRequiredData>), nameof(IModuleContainerFor<PrefireRequiredData>.Instance))),
            new CodeInstruction(OpCodes.Stloc, lbd_data_module),
            new CodeInstruction(OpCodes.Ldloc, lbd_data_module),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PrefireRequiredData), nameof(PrefireRequiredData.prefireRequired))),
            new CodeInstruction(OpCodes.Stloc, lbd_prefire),
            new CodeInstruction(OpCodes.Ldloc_S, lbd_prefire),
            new CodeInstruction(OpCodes.Brfalse_S, lbl_vanilla),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Brtrue_S, lbl_vanilla),
            new CodeInstruction(OpCodes.Ldloc_S, lbd_data_module),
            CodeInstruction.LoadField(typeof(PrefireRequiredData), nameof(PrefireRequiredData.prefireStartTime)),
            new CodeInstruction(OpCodes.Ldc_R4, 0),
            new CodeInstruction(OpCodes.Ble_S, lbl_vanilla),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Time), nameof(Time.time))),
            CodeInstruction.StoreField(typeof(ItemActionData), nameof(ItemActionData.lastUseTime))
        });

        return codes;
    }


    public class PrefireRequiredData
    {
        public bool prefireRequired = true;
        public float prefireStartTime = -1f;
    }
}
