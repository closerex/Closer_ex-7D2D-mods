using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using KFCommonUtilityLib.Scripts.Utilities;
using System.Collections.Generic;
using UniLinq;
using System.Reflection.Emit;

[TypeTarget(typeof(ItemActionDynamic)), TypeDataTarget(typeof(LimitedComboData))]
public class ActionModuleLimitedCombo
{
    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemAction __instance, ItemActionData _data, LimitedComboData __customData)
    {
        int actionIndex = _data.indexInEntityOfAction;
        string originalValue = false.ToString();
        __instance.Properties.ParseString("MaxComboCount", ref originalValue);
        __customData.maxCombo = int.Parse(_data.invData.itemValue.GetPropertyOverrideForAction("MaxComboCount", originalValue, actionIndex));

        __customData.ResetCombo();
    }

    [HarmonyPatch(nameof(ItemAction.StopHolding)), MethodTargetPostfix]
    public void Postfix_StopHolding(LimitedComboData __customData)
    {
        __customData.ResetCombo();
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPrefix]
    public bool Prefix_ExecuteAction(bool _bReleased, LimitedComboData __customData)
    {
        if (_bReleased)
        {
            __customData.ResetCombo();
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.ExecuteAction)), MethodTargetTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionDynamicMelee_ExecuteAction(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        var fld_atk = AccessTools.Field(typeof(ItemActionDynamicMelee.ItemActionDynamicMeleeData), nameof(ItemActionDynamicMelee.ItemActionDynamicMeleeData.Attacking));

        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].StoresField(fld_atk) && codes[i - 1].opcode == OpCodes.Ldc_I4_1)
            {
                Label lbl_pop = generator.DefineLabel(), lbl_ret = generator.DefineLabel();
                var code_original = codes[i - 2];
                codes.InsertRange(i - 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_1).WithLabels(code_original.ExtractLabels()),
                    new CodeInstruction(OpCodes.Isinst, typeof(IModuleContainerFor<LimitedComboData>)),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl_pop),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(IModuleContainerFor<LimitedComboData>), nameof(IModuleContainerFor<LimitedComboData>.Instance))),
                    CodeInstruction.Call(typeof(LimitedComboData), nameof(LimitedComboData.AddCombo)),
                    new CodeInstruction(OpCodes.Br_S, lbl_ret),
                    new CodeInstruction(OpCodes.Pop).WithLabels(lbl_pop)
                });
                code_original.WithLabels(lbl_ret);
                break;
            }
        }


        return codes;
    }

    public class LimitedComboData
    {
        public int maxCombo = 3;
        public int currentCombo = 0;

        public void ResetCombo()
        {
            currentCombo = 0;
        }

        public bool CanContinue()
        {
            return currentCombo < maxCombo;
        }

        public void AddCombo()
        {
            currentCombo++;
        }
    }
}

[HarmonyPatch]
public static class ActionModuleLimitedComboPatches
{
    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemActionDynamicMelee.canStartAttack))]
    [HarmonyPrefix]
    private static bool Prefix_canStartAttack_ItemActionDynamicMelee(ItemActionData _actionData, ref bool __result)
    {
        if (_actionData is IModuleContainerFor<ActionModuleLimitedCombo.LimitedComboData> dataModule)
        {
            var customData = dataModule.Instance;
            if (!customData.CanContinue())
            {
                //_actionData.lastUseTime = Time.time;
                __result = false;
                return false;
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(ItemClass), nameof(ItemClass.ExecuteAction))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ExecuteAction_ItemClass(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_1 && codes[i - 1].opcode == OpCodes.Ldarg_3)
            {
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_2).WithLabels(codes[i + 1].ExtractLabels()),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_3),
                    CodeInstruction.Call(typeof(ActionModuleLimitedComboPatches), nameof(ResetCombo))
                });

                break;
            }
        }

        return codes;
    }

    private static void ResetCombo(ItemInventoryData invData, int actionIdx, bool released)
    {
        if (!released)
        {
            return;
        }
        if (invData.actionData[actionIdx] is IModuleContainerFor<ActionModuleLimitedCombo.LimitedComboData> dataModule)
        {
            dataModule.Instance.ResetCombo();
        }
    }
}
