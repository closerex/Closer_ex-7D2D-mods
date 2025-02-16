﻿using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;

[TypeTarget(typeof(ItemAction)), ActionDataTarget(typeof(AnimationLockedData))]
public class ActionModuleAnimationLocked
{
    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    private void Postfix_StartHolding(AnimationLockedData __customData)
    {
        __customData.isLocked = false;
        __customData.isReloadLocked = false;
    }

    [HarmonyPatch(nameof(ItemAction.IsActionRunning)), MethodTargetPostfix]
    private void Postfix_IsActionRunning(AnimationLockedData __customData, ref bool __result)
    {
        __result |= __customData.isLocked;
    }

    [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.CanReload)), MethodTargetPostfix]
    private void Postfix_CanReload_ItemActionAttack(AnimationLockedData __customData, ref bool __result)
    {
        __result &= !__customData.isReloadLocked;
    }

    public class AnimationLockedData
    {
        public bool isLocked = false;
        public bool isReloadLocked = false;

        public AnimationLockedData(ItemInventoryData invData, int actionIndex, ActionModuleAnimationLocked module)
        {
            
        }
    }
}
