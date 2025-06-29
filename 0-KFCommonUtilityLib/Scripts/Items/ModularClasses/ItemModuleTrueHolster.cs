using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;
using System.Collections;
using UnityEngine;

[TypeTarget(typeof(ItemClass)), TypeDataTarget(typeof(TrueHolsterData))]
public class ItemModuleTrueHolster
{
    public bool holsterDelayActive = true;
    public bool unholsterDelayActive = true;

    [HarmonyPatch(nameof(ItemClass.Init)), MethodTargetPostfix]
    public void Postfix_Init(ItemClass __instance)
    {
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
    [HarmonyPatch(nameof(ItemClass.OnHoldingReset))]
    [MethodTargetPostfix]
    public void Postfix_Reset(ItemClass __instance, TrueHolsterData __customData)
    {
        __customData.IsHolstering = false;
        __customData.IsUnholstering = false;
    }

    public void Postfix_OnHoldingReset(ItemClass __instance, TrueHolsterData __customData)
    {
        __customData.IsHolstering = false;
        __customData.IsUnholstering = false;
    }

    [HarmonyPatch(nameof(ItemClass.IsActionRunning)), MethodTargetPostfix]
    public void Postfix_IsActionRunning(ItemClass __instance, ref bool __result, TrueHolsterData __customData)
    {
        __result |= __customData.IsSwapping;
    }

    public class TrueHolsterData
    {
        public bool IsSwapping => IsHolstering || IsUnholstering;
        public bool IsHolstering { get; internal set; }
        public bool IsUnholstering { get; internal set; }

        public ItemModuleTrueHolster module;

        public TrueHolsterData(ItemInventoryData _invData, ItemClass _item, ItemStack _itemStack, IGameManager _gameManager, EntityAlive _holdingEntity, int _slotIdx, ItemModuleTrueHolster module)
        {
            this.module = module;
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
