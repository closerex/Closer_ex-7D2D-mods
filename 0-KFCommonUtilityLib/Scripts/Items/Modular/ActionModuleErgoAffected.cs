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
        float ergoValue = EffectManager.GetValue(CustomEnums.WeaponErgonomics, _actionData.invData.itemValue, 0, holdingEntity);
        float aimSpeedModifier = Mathf.Lerp(0.2f, 1, ergoValue);
        Log.Out($"Ergo is {ergoValue}, aim speed is {aimSpeedModifier}");
        holdingEntity.emodel.avatarController.UpdateFloat(AimSpeedModifierHash, aimSpeedModifier, true);
        holdingEntity.MinEventContext.ItemActionData = prevActionData;
    }
}
