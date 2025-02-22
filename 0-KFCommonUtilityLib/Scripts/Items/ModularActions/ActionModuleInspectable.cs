using HarmonyLib;
using KFCommonUtilityLib.Scripts.Attributes;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleInspectable
{
    public bool allowEmptyInspect;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        allowEmptyInspect = _props.GetBool("allowEmptyInspect");
    }

    [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemAction.CancelAction)), MethodTargetPostfix]
    private void Postfix_CancelAction_ItemActionDynamic(ItemActionDynamic.ItemActionDynamicData _actionData)
    {
        var entity = _actionData.invData.holdingEntity;
        if (!entity.MovementRunning && _actionData != null && !entity.inventory.holdingItem.IsActionRunning(entity.inventory.holdingItemData))
        {
            entity.emodel.avatarController._setTrigger("weaponInspect", false);
        }
    }
}
