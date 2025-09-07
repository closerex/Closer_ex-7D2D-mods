using HarmonyLib;
using KFCommonUtilityLib.Attributes;
using System.Collections;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleHoldOpen
{
    private const string emptyAnimatorBool = "empty";
    private int emptyAnimatorBoolHash;

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
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

    [HarmonyPatch(nameof(ItemActionRanged.getUserData)), MethodTargetPostfix]
    public void Postfix_getUserData(ItemActionData _actionData, ref int __result)
    {
        __result |= (_actionData.invData.itemValue.Meta <= 0 ? 1 : 0);
    }

    [HarmonyPatch(nameof(ItemAction.ItemActionEffects)), MethodTargetPostfix]
    public void Postfix_ItemActionEffects(ItemActionData _actionData, int _firingState, int _userData)
    {
        if (_firingState != (int)ItemActionFiringState.Off && (_userData & 1) > 0)
            _actionData.invData.holdingEntity.emodel.avatarController.UpdateBool(emptyAnimatorBoolHash, true, false);
    }

    [HarmonyPatch(nameof(ItemActionRanged.ReloadGun)), MethodTargetPostfix]
    public void Postfix_ReloadGun(ItemActionData _actionData)
    {
        //delay 2 frames before reloading, since the animation is likely to be triggered the next frame this is called
        ThreadManager.StartCoroutine(DelaySetEmpty(_actionData, false, 2));
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPrefix]
    public bool Prefix_StartHolding(ItemActionData _data)
    {
        //delay 1 frame before equipping weapon
        if (_data.invData.itemValue.Meta <= 0)
            ThreadManager.StartCoroutine(DelaySetEmpty(_data, true, 2));
        return true;
    }

    [HarmonyPatch(nameof(ItemActionRanged.ConsumeAmmo)), MethodTargetPostfix]
    public void Postfix_ConsumeAmmo(ItemActionData _actionData)
    {
        if (_actionData.invData.itemValue.Meta == 0)
            _actionData.invData.holdingEntity.FireEvent(CustomEnums.onSelfMagzineDeplete, true);
    }

    [HarmonyPatch(nameof(ItemAction.SwapAmmoType)), MethodTargetPrefix]
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