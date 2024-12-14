using KFCommonUtilityLib.Scripts.Attributes;
using KFCommonUtilityLib.Scripts.StaticManagers;
using UnityEngine;

[TypeTarget(typeof(ItemActionZoom))]
public class ActionModuleErgoAffected
{
    public static readonly int AimSpeedModifierHash = Animator.StringToHash("AimSpeedModifier");

    [MethodTargetPostfix(nameof(ItemAction.ExecuteAction))]
    private void Postfix_ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        ItemActionData prevActionData = holdingEntity.MinEventContext.ItemActionData;
        holdingEntity.MinEventContext.ItemActionData = _actionData.invData.actionData[MultiActionManager.GetActionIndexForEntity(holdingEntity)];
        float aimSpeedModifier = Mathf.Lerp(0.2f, 1, EffectManager.GetValue(CustomEnums.WeaponErgonomics, _actionData.invData.itemValue, 0, holdingEntity));
        holdingEntity.emodel.avatarController.UpdateFloat(AimSpeedModifierHash, aimSpeedModifier, true);
        holdingEntity.MinEventContext.ItemActionData = prevActionData;
    }
}
