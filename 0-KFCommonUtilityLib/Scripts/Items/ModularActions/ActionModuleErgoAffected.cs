using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using static ActionModuleErgoAffected;

[TypeTarget(typeof(ItemActionZoom)), TypeDataTarget(typeof(ErgoData))]
public class ActionModuleErgoAffected
{
    public static readonly int AimSpeedModifierHash = Animator.StringToHash("AimSpeedModifier");
    public float zoomInTimeBase;
    public float aimSpeedModifierBase;

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    private void Postfix_OnModificationChanged(ItemActionData _data, ItemActionZoom __instance, ErgoData __customData)
    {
        zoomInTimeBase = 0.3f;
        __instance.Properties.ParseFloat("ZoomInTimeBase", ref zoomInTimeBase);
        aimSpeedModifierBase = 1f;
        __instance.Properties.ParseFloat("AimSpeedModifierBase", ref aimSpeedModifierBase);
        __customData.minErgo = 0.2f;
        __instance.Properties.ParseFloat("MinErgoPerc", ref __customData.minErgo);
        __customData.minErgo = float.Parse(_data.invData.itemValue.GetPropertyOverride("MinErgoPerc", __customData.minErgo.ToString()));
        __customData.aimStartTime = float.MaxValue;
        __customData.aimSet = false;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    private void Postfix_ExecuteAction(ItemActionData _actionData, ItemActionZoom __instance, bool _bReleased, ErgoData __customData)
    {
        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        ItemActionData prevActionData = holdingEntity.MinEventContext.ItemActionData;
        holdingEntity.MinEventContext.ItemActionData = _actionData.invData.actionData[MultiActionManager.GetActionIndexForEntity(holdingEntity)];
        __customData.curErgo = EffectManager.GetValue(CustomEnums.WeaponErgonomics, _actionData.invData.itemValue, 0, holdingEntity);
        float aimSpeedModifier = __customData.ModifiedErgo;
        Log.Out($"Ergo is {__customData.curErgo}, base aim modifier is {aimSpeedModifierBase}, aim speed is {aimSpeedModifier * aimSpeedModifierBase}");
        holdingEntity.emodel.avatarController.UpdateFloat(AimSpeedModifierHash, aimSpeedModifier * aimSpeedModifierBase, true);
        holdingEntity.MinEventContext.ItemActionData = prevActionData;
        if ((_actionData as ItemActionZoom.ItemActionDataZoom).aimingValue && !_bReleased)
        {
            __customData.aimStartTime = Time.time;
        }
        else if (!(_actionData as ItemActionZoom.ItemActionDataZoom).aimingValue)
        {
            __customData.aimStartTime = float.MaxValue;
        }
        __customData.aimSet = false;
    }

    public class ErgoData
    {
        public float aimStartTime;
        public bool aimSet;
        public ActionModuleErgoAffected module;
        public float curErgo;
        public float minErgo = 0.2f;

        public float ModifiedErgo => Mathf.Lerp(minErgo, 1, curErgo);

        public ErgoData(ActionModuleErgoAffected __customModule)
        {
            aimStartTime = float.MaxValue;
            aimSet = false;
            module = __customModule;
        }
    }
}

[HarmonyPatch]
public static class ErgoPatches
{
    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.updateAccuracy))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler_ItemActionRanged_updateAccuracy(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Stloc_3)
            {
                var lbl = generator.DefineLabel();
                codes[i + 1].WithLabels(lbl);
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Dup),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.lastAccuracy), true),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    CodeInstruction.Call(typeof(ErgoPatches), nameof(CalcErgoModifier)),
                    new CodeInstruction(OpCodes.Brfalse_S, lbl),
                    CodeInstruction.LoadField(typeof(ItemActionRanged.ItemActionDataRanged), nameof(ItemActionRanged.ItemActionDataRanged.lastAccuracy)),
                    new CodeInstruction(OpCodes.Ret)
                });
                break;
            }
        }

        return codes;
    }

    private static bool CalcErgoModifier(ItemActionData actionData, ref float accuracy, float targetValue, bool aiming)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = actionData as ItemActionRanged.ItemActionDataRanged;
        if (aiming && rangedData.invData.actionData[1] is IModuleContainerFor<ActionModuleErgoAffected.ErgoData> dataModule && !dataModule.Instance.aimSet && Time.time - dataModule.Instance.aimStartTime > 0)
        {
            ActionModuleErgoAffected.ErgoData ergoData = dataModule.Instance;
            float baseAimTime = ergoData.module.zoomInTimeBase;
            float baseAimMultiplier = ergoData.module.aimSpeedModifierBase;
            baseAimTime /= baseAimMultiplier;
            //float modifiedErgo = EffectManager.GetValue(CustomEnums.WeaponErgonomics, rangedData.invData.itemValue, 1f, rangedData.invData.holdingEntity);
            float modifiedErgo = ergoData.ModifiedErgo;
            float perc = (Time.time - ergoData.aimStartTime) * modifiedErgo / baseAimTime;
            if (perc >= 1)
            {
                ergoData.aimSet = true;
                perc = 1;
            }
            accuracy = Mathf.Lerp(accuracy, targetValue, perc);
            //Log.Out($"Time passed {Time.time - dataModule.Instance.aimStartTime} base time {baseAimTime} perc {perc}");
            return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.onHoldingEntityFired))]
    [HarmonyPrefix]
    private static bool Prefix_onHoldingEntityFired_ItemActionRanged(ItemActionData _actionData, out float __state)
    {
        __state = (_actionData as ItemActionRanged.ItemActionDataRanged).lastAccuracy;
        return true;
    }

    [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.onHoldingEntityFired))]
    [HarmonyPostfix]
    private static void Postfix_onHoldingEntityFired_ItemActionRanged(ItemActionData _actionData, float __state)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = _actionData as ItemActionRanged.ItemActionDataRanged;
        if (rangedData.invData.holdingEntity.AimingGun && rangedData.invData.actionData[1] is IModuleContainerFor<ActionModuleErgoAffected.ErgoData> dataModule)
        {
            float aimMultiplier = EffectManager.GetValue(PassiveEffects.SpreadMultiplierAiming, rangedData.invData.itemValue, .1f, rangedData.invData.holdingEntity);
            rangedData.lastAccuracy = Mathf.Lerp(__state, rangedData.lastAccuracy, aimMultiplier);
            ActionModuleErgoAffected.ErgoData ergoData = dataModule.Instance;
            if (Time.time > ergoData.aimStartTime)
            {
                ergoData.aimSet = false;
                ergoData.aimStartTime = Time.time;
            }
        }
    }
}
