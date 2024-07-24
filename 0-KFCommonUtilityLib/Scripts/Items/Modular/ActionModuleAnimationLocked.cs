using KFCommonUtilityLib.Scripts.Attributes;

[TypeTarget(typeof(ItemAction), typeof(AnimationLockedData))]
public class ActionModuleAnimationLocked
{
    [MethodTargetPostfix(nameof(ItemAction.StartHolding))]
    private void Postfix_StartHolding(AnimationLockedData __customData)
    {
        __customData.isLocked = false;
        __customData.isReloadLocked = false;
    }

    [MethodTargetPostfix(nameof(ItemAction.IsActionRunning))]
    private void Postfix_IsActionRunning(AnimationLockedData __customData, ref bool __result)
    {
        __result |= __customData.isLocked;
    }

    [MethodTargetPostfix(nameof(ItemActionAttack.CanReload), typeof(ItemActionAttack))]
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
