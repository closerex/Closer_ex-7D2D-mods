using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using System.Collections;
using UnityEngine;

[TypeTarget(typeof(ItemClass)), TypeDataTarget(typeof(TrueHolsterData))]
public class ItemModuleTrueHolster
{
    public bool holsterDelayActive = true;
    public bool unholsterDelayActive = true;
    public ItemClass item;

    [HarmonyPatch(nameof(ItemClass.Init)), MethodTargetPostfix]
    public void Postfix_Init(ItemClass __instance)
    {
        item = __instance;
        __instance.Properties.ParseBool("HolsterDelayActive", ref holsterDelayActive);
        __instance.Properties.ParseBool("UnholsterDelayActive", ref unholsterDelayActive);
    }

    [HarmonyPatch(nameof(ItemClass.StartHolding)), MethodTargetPrefix]
    public bool Prefix_StartHolding(ItemClass __instance, TrueHolsterData __customData, Transform _modelTransform)
    {
        if (unholsterDelayActive && _modelTransform.TryGetComponent<AnimationTargetsAbs>(out var targets) && targets.IsAnimationSet)
        {
            __customData.IsUnholstering = true;
        }
        else
        {
            __customData.IsUnholstering = false;
        }
        __customData.IsHolstering = false;
        return true;
    }

    [HarmonyPatch(nameof(ItemClass.StopHolding))]
    [MethodTargetPostfix]
    public void Postfix_Reset(ItemClass __instance, TrueHolsterData __customData)
    {
        __customData.IsHolstering = false;
        __customData.IsUnholstering = false;
    }

    [HarmonyPatch(nameof(ItemClass.OnHoldingReset))]
    [MethodTargetPostfix]
    public void Postfix_OnHoldingReset(ItemClass __instance, ItemInventoryData _data, TrueHolsterData __customData)
    {
        __customData.IsHolstering = false;
        if (_data.holdingEntity && _data.holdingEntity.emodel.avatarController)
        {
            _data.holdingEntity.emodel.avatarController.CancelEvent(AvatarController.itemHasChangedTriggerHash);
        }
    }

    [HarmonyPatch(nameof(ItemClass.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(ItemClass __instance, ref bool __result, TrueHolsterData __customData)
    {
        __result |= __customData.IsSwapping;
    }

    public class TrueHolsterData
    {
        public bool IsSwapping => IsHolstering || IsUnholstering;
        public bool IsHolstering
        {
            get { return isHolstering; }

            internal set
            {
                //Log.Out($"TrueHolsterData: IsHolstering set from {IsHolstering} to {value} for item {module.item.Name}\n{StackTraceUtility.ExtractStackTrace()}");
                isHolstering = value;
            }
        }
        public bool IsUnholstering
        {
            get { return isUnholstering; }

            internal set
            {
                //Log.Out($"TrueHolsterData: IsUnholstering set from {isUnholstering} to {value} for item {module.item.Name}\n{StackTraceUtility.ExtractStackTrace()}");
                isUnholstering = value;
            }
        }

        public ItemModuleTrueHolster module;
        private bool isHolstering;
        private bool isUnholstering;

        public TrueHolsterData(ItemModuleTrueHolster __customModule)
        {
            this.module = __customModule;
        }

        public IEnumerator WaitForHolster()
        {
            while (IsHolstering)
            {
                yield return null;
            }
        }

        public IEnumerator WaitForUnholster()
        {
            while (IsUnholstering)
            {
                yield return null;
            }
        }
    }
}
