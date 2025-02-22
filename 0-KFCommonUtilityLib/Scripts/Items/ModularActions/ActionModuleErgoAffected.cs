using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using System.Collections.Generic;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;

[TypeTarget(typeof(ItemActionZoom)), ActionDataTarget(typeof(ErgoData))]
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
        __customData.aimStartTime = float.MaxValue;
        __customData.aimSet = false;
    }

    [HarmonyPatch(nameof(ItemAction.ExecuteAction)), MethodTargetPostfix]
    private void Postfix_ExecuteAction(ItemActionData _actionData, ItemActionZoom __instance, bool _bReleased, ErgoData __customData)
    {
        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        ItemActionData prevActionData = holdingEntity.MinEventContext.ItemActionData;
        holdingEntity.MinEventContext.ItemActionData = _actionData.invData.actionData[MultiActionManager.GetActionIndexForEntity(holdingEntity)];
        float ergoValue = EffectManager.GetValue(CustomEnums.WeaponErgonomics, _actionData.invData.itemValue, 0, holdingEntity);
        float aimSpeedModifier = Mathf.Lerp(0.2f, 1, ergoValue);
        Log.Out($"Ergo is {ergoValue}, base aim modifier is {aimSpeedModifierBase}, aim speed is {aimSpeedModifier * aimSpeedModifierBase}");
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

    //[MethodTargetPostfix(nameof(ItemAction.OnHoldingUpdate))]
    //private void Postfix_OnHoldingUpdate(ItemActionData _actionData, ErgoData __customData)
    //{
    //    if ((_actionData as ItemActionZoom.ItemActionDataZoom).aimingValue && Time.time - __customData.aimStartTime > zoomInTimeBase)
    //    {
    //        __customData.aimSet = true;
    //    }
    //    else
    //    {
    //        __customData.aimSet = false;
    //    }
    //}

    public class ErgoData
    {
        public float aimStartTime;
        public bool aimSet;
        public ActionModuleErgoAffected module;
        public ErgoData(ItemInventoryData _invData, int _indexInEntityOfAction, ActionModuleErgoAffected _module)
        {
            aimStartTime = float.MaxValue;
            aimSet = false;
            module = _module;
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
        var mtd_lerp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Lerp), new[] { typeof(float), typeof(float), typeof(float) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(mtd_lerp))
            {
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    CodeInstruction.Call(typeof(ErgoPatches), nameof(CalcErgoModifier)),
                });
                break;
            }
        }

        return codes;
    }

    private static float CalcErgoModifier(float originalValue, ItemAction action, ItemActionData actionData, bool aiming)
    {
        ItemActionRanged.ItemActionDataRanged rangedData = actionData as ItemActionRanged.ItemActionDataRanged;
        if (aiming && rangedData.invData.actionData[1] is IModuleContainerFor<ActionModuleErgoAffected.ErgoData> dataModule && !dataModule.Instance.aimSet && Time.time - dataModule.Instance.aimStartTime > 0)
        {
            ActionModuleErgoAffected.ErgoData ergoData = dataModule.Instance;
            float baseAimTime = ergoData.module.zoomInTimeBase;
            float baseAimMultiplier = ergoData.module.aimSpeedModifierBase;
            baseAimTime /= baseAimMultiplier;
            float modifiedErgo = EffectManager.GetValue(CustomEnums.WeaponErgonomics, rangedData.invData.itemValue, 1f, rangedData.invData.holdingEntity);
            modifiedErgo = Mathf.Lerp(0.2f, 1, modifiedErgo);
            float perc = (Time.time - ergoData.aimStartTime) * modifiedErgo / baseAimTime;
            if (perc >= 1)
            {
                ergoData.aimSet = true;
                perc = 1;
            }
            //Log.Out($"Time passed {Time.time - dataModule.Instance.aimStartTime} base time {baseAimTime} perc {perc}");
            return perc;
        }
        return originalValue;
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
