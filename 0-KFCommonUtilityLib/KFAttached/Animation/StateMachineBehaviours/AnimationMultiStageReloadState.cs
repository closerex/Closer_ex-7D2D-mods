﻿#if NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
using UAI;

#endif
using UnityEngine;

public class AnimationMultiStageReloadState : StateMachineBehaviour
{
    [SerializeField]
    private bool speedUpOnCancel;
    [SerializeField]
    private bool immediateCancel;
    [SerializeField]
    private float ForceCancelReloadDelay = 1f;
    [SerializeField]
    private bool DoNotForceCancel = false;

    private AnimationReloadEvents eventBridge;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("Reload", false);
        if (eventBridge == null)
        {
            eventBridge = animator.GetComponent<AnimationReloadEvents>();
        }
        if (stateInfo.IsTag("ReloadStart"))
        {
            animator.speed = 1f;
            animator.SetBool("IsReloading", true);
#if NotEditor
            EntityPlayerLocal player = animator.GetComponentInParent<EntityPlayerLocal>();
            int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
            eventBridge.OnReloadStart(actionIndex);
#if DEBUG
            Log.Out($"start reload {actionIndex}");
#endif
#endif
        }
    }

#if NotEditor
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var actionData = eventBridge.actionData;
        if (actionData == null)
        {
            return;
        }

        if (actionData.isReloadCancelled)
        {
            if (speedUpOnCancel)
            {
                Log.Out("Speed up animation!");
                animator.speed = 30;
            }

            if (immediateCancel)
            {
                animator.SetBool("IsReloading", false);
            }

            if (!DoNotForceCancel)
            {
                eventBridge.DelayForceCancelReload(ForceCancelReloadDelay);
            }
        }

        if (actionData.isReloading && animator.GetBool("IsReloading"))
        {
            actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
            actionData.invData.holdingEntity.FireEvent(MinEventTypes.onReloadUpdate, true);
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.speed = 1f;
        if (stateInfo.IsTag("ReloadEnd"))
            eventBridge?.OnReloadFinish();
    }
#endif
}