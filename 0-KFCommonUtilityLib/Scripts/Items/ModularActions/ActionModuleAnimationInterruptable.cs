using HarmonyLib;
using KFCommonUtilityLib;
using KFCommonUtilityLib.Attributes;
using UnityEngine;

[TypeTarget(typeof(ItemAction)), TypeDataTarget(typeof(AnimationInterruptableData))]
public class ActionModuleAnimationInterruptable
{
    public string interruptStateName = "";
    public static int powerAttackHash = Animator.StringToHash("PowerAttack");

    [HarmonyPatch(nameof(ItemAction.ReadFrom)), MethodTargetPostfix]
    public void Postfix_ReadFrom(DynamicProperties _props)
    {
        interruptStateName = _props.GetString("InterruptStateFullName");
    }

    [HarmonyPatch(nameof(ItemAction.StartHolding)), MethodTargetPostfix]
    public void Postfix_StartHolding(AnimationInterruptableData __customData)
    {
        __customData.interruptRequested = false;
    }

    [HarmonyPatch(nameof(ItemAction.OnModificationsChanged)), MethodTargetPostfix]
    public void Postfix_OnModificationsChanged(ItemActionData _data, AnimationInterruptableData __customData)
    {
        __customData.targets = AnimationRiggingManager.GetHoldingRigTargetsFromPlayer(_data.invData.holdingEntity);
        if (__customData.targets && __customData.targets.IsAnimationSet)
        {
            __customData.animator = __customData.targets.GraphBuilder.WeaponWrapper;
        }
        __customData.interruptRequested = false;
    }

    [HarmonyPatch(typeof(ItemActionDynamicMelee), nameof(ItemAction.CancelAction)), MethodTargetPostfix]
    public void Postfix_ItemActionDynamicMelee_CancelAction(ItemActionDynamicMelee __instance, ItemActionData _actionData, AnimationInterruptableData __customData)
    {
        if (__instance.IsActionRunning(_actionData) && __customData.interruptRequested && __customData.IsInterruptable())
        {
            var controller = _actionData.invData.holdingEntity?.emodel?.avatarController;
            if (controller != null)
            {
                controller.CancelEvent(__instance.UsePowerAttackAnimation ? powerAttackHash : AvatarController.weaponFireHash);
            }
            __customData.animator.Play(interruptStateName, -1, 0f);
            //__customData.animator.Update(0f);
            __instance.SetAttackFinished(_actionData);
            _actionData.lastUseTime = 0f;
        }
        __customData.interruptRequested = false;
    }

    public class AnimationInterruptableData
    {
        public AnimationTargetsAbs targets;
        public IAnimatorWrapper animator;
        public bool interruptRequested;

        public bool IsInterruptable()
        {
            return targets && targets.IsAnimationSet && animator.IsValid;
        }
    }
}
