using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using static ActionModuleInspectable;

[TypeTarget(typeof(ItemActionDynamicMelee))]
public class ActionModuleAltMeleeInspectTrigger
{
    [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemAction.CancelAction)), MethodTargetPostfix]
    public void Postfix_ItemActionDynamic_CancelAction(ItemActionDynamic.ItemActionDynamicData _actionData)
    {
        var entity = _actionData.invData.holdingEntity;
        if (entity is EntityPlayerLocal player && player.inventory.GetIsFinishedSwitchingHeldItem() && player.inventory.holdingItemData.IsModuleContainerFor(out ItemModuleMultiItem.MultiItemInvData multiInvData))
        {
            ItemModuleMultiItem.CheckAltMelee(player, multiInvData, false, 1, true);
            ItemModuleMultiItem.CheckAltMelee(player, multiInvData, true, 1, true);
        }
    }
}