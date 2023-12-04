using KFCommonUtilityLib.Scripts.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[TypeTarget(typeof(ItemActionRanged))]
public class ActionModuleHoldOpen
{
    private const string emptyAnimatorBool = "empty";

    public Animator getAnimator(EntityAlive holdingEntity)
    {
        Animator animator = null;
        //should not use ?. here because when you use something from bag ui entry, the holding item is destroyed but still referenced in the avatar controller
        //and ?. will try to access that reference instead of return null and throw NRE, while != in unity is override to return null in such case
        if (holdingEntity.emodel.avatarController is AvatarMultiBodyController multiBody && multiBody.HeldItemAnimator != null)
            animator = multiBody.HeldItemAnimator;
        else if (holdingEntity.emodel.avatarController is LegacyAvatarController legacy && legacy.HeldItemTransform != null)
            animator = legacy.HeldItemTransform.GetComponent<Animator>();
        return animator;
    }

    public void setAnimatorBool(EntityAlive holdingEntity, string parameter, bool flag)
    {
        holdingEntity.emodel.avatarController.UpdateBool(parameter, flag, false);
        //Animator animator = getAnimator(holdingEntity);
        //if (animator)
        //{
        //    animator.SetBool(parameter, flag);
        //    //Log.Out("trying to set param: " + parameter + " flag: " + flag + " result: " + getAnimatorBool(holdingEntity, parameter) + " transform: " + animator.transform.name);
        //}
    }

    public void setAnimatorFloat(EntityAlive holdingEntity, string parameter, float value)
    {
        holdingEntity.emodel.avatarController.UpdateFloat(parameter, value, false);
        //Animator animator = getAnimator(holdingEntity);
        //if (animator)
        //{
        //    animator.SetFloat(parameter, value);
        //    //Log.Out("trying to set param: " + parameter + " flag: " + flag + " result: " + getAnimatorBool(holdingEntity, parameter) + " transform: " + animator.transform.name);
        //}
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
            setAnimatorBool(_actionData.invData.holdingEntity, emptyAnimatorBool, true);
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
            ThreadManager.StartCoroutine(DelaySetEmpty(_data, true, 1));
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
        setAnimatorBool(_entity, emptyAnimatorBool, true);
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
            setAnimatorBool(_actionData.invData.holdingEntity, emptyAnimatorBool, empty);
        }
        yield break;
    }
}