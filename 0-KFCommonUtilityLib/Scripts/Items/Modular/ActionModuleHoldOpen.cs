using KFCommonUtilityLib.Scripts.Attributes;
using System.Collections;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleHoldOpen
{
    private const string emptyAnimatorBool = "empty";
    private int emptyAnimatorBoolHash;

    [MethodTargetPostfix(nameof(ItemActionRanged.ReadFrom))]
    private void Postfix_ReadFrom(DynamicProperties _props, ItemActionRanged __instance)
    {
        int metaIndex = __instance.ActionIndex;
        if (_props.Values.TryGetValue("ShareMetaWith", out string str) && int.TryParse(str, out metaIndex))
        {

        }
        if (metaIndex > 0)
        {
            emptyAnimatorBoolHash = Animator.StringToHash(emptyAnimatorBool + __instance.ActionIndex);
        }
        else
        {
            emptyAnimatorBoolHash = Animator.StringToHash(emptyAnimatorBool);
        }
    }

    [MethodTargetPostfix("getUserData")]
    public void Postfix_getUserData(ItemActionData _actionData, ref int __result)
    {
        __result |= (_actionData.invData.itemValue.Meta <= 0 ? 1 : 0);
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.ItemActionEffects))]
    public void Postfix_ItemActionEffects(ItemActionData _actionData, int _firingState, int _userData)
    {
        if (_firingState != (int)ItemActionFiringState.Off && (_userData & 1) > 0)
            _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(emptyAnimatorBoolHash, true, false);
    }

    [MethodTargetPostfix(nameof(ItemActionRanged.ReloadGun))]
    public void Postfix_ReloadGun(ItemActionData _actionData)
    {
        //delay 2 frames before reloading, since the animation is likely to be triggered the next frame this is called
        ThreadManager.StartCoroutine(DelaySetEmpty(_actionData, false, 2));
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.StartHolding))]
    public bool Prefix_StartHolding(ItemActionData _data)
    {
        //delay 1 frame before equipping weapon
        if (_data.invData.itemValue.Meta <= 0)
            ThreadManager.StartCoroutine(DelaySetEmpty(_data, true, 2));
        return true;
    }

    [MethodTargetPostfix("ConsumeAmmo")]
    public void Postfix_ConsumeAmmo(ItemActionData _actionData)
    {
        if (_actionData.invData.itemValue.Meta == 0)
            _actionData.invData.holdingEntity.FireEvent(CustomEnums.onSelfMagzineDeplete, true);
    }

    [MethodTargetPrefix(nameof(ItemActionRanged.SwapAmmoType))]
    public bool Prefix_SwapAmmoType(EntityAlive _entity)
    {
        _entity.emodel.avatarController.UpdateBool(emptyAnimatorBoolHash, true, false);
        return true;
    }

    private IEnumerator DelaySetEmpty(ItemActionData _actionData, bool empty, int delay)
    {
        for (int i = 0; i < delay; i++)
        {
            yield return null;
        }
        if (_actionData.invData.holdingEntity.inventory.holdingItemIdx == _actionData.invData.slotIdx)
        {
            _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(emptyAnimatorBoolHash, empty, false);
        }
        yield break;
    }
}