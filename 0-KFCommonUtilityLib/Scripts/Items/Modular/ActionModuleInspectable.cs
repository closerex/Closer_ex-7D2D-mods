using KFCommonUtilityLib.Scripts.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[TypeTarget(typeof(ItemAction))]
public class ActionModuleInspectable
{
    public bool allowEmptyInspect;

    [MethodTargetPostfix(nameof(ItemAction.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props)
    {
        allowEmptyInspect = _props.GetBool("allowEmptyInspect");
    }

    [MethodTargetPostfix(nameof(ItemAction.CancelAction), typeof(ItemActionDynamic))]
    private void Postfix_CancelAction_ItemActionDynamic(ItemActionDynamic.ItemActionDynamicData _actionData)
    {
        var entity = _actionData.invData.holdingEntity;
        if (!entity.MovementRunning && _actionData != null && !entity.inventory.holdingItem.IsActionRunning(entity.inventory.holdingItemData))
        {
            entity.emodel.avatarController._setTrigger("weaponInspect", false);
        }
    }
}
