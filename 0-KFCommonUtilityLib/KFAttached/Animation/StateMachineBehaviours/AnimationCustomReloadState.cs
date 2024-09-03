﻿#if NotEditor
using KFCommonUtilityLib.Scripts.StaticManagers;
#endif
using UnityEngine;

public class AnimationCustomReloadState : StateMachineBehaviour
{
    [SerializeField]
    private float ForceCancelReloadDelay = 1f;
    [SerializeField]
    private bool DoNotForceCancel = false;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.speed = 1f;
        animator.SetBool("Reload", false);
        animator.SetBool("IsReloading", true);
#if NotEditor
        if (player == null)
        {
            player = animator.GetComponentInParent<EntityPlayerLocal>();
        }
        int actionIndex = MultiActionManager.GetActionIndexForEntity(player);
#if DEBUG
        Log.Out($"start reload {actionIndex}");
#endif
        actionData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
        if (eventBridge == null)
        {
            eventBridge = animator.GetComponent<AnimationReloadEvents>();
        }

#if DEBUG
        Log.Out($"ANIMATOR STATE ENTER : {actionData.invData.item.Name}");
#endif
        eventBridge.OnReloadStart(actionIndex);
        //eventBridge.OnReloadUpdate();
#endif
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.speed = 1f;
#if NotEditor
        //eventBridge.OnReloadUpdate();
        if (actionData == null)
        {
            return;
        }
        if (actionData.isReloading)
        {
            eventBridge.OnReloadFinish();
        }
#endif
        //actionData.isReloading = false;
        //actionData.isReloadCancelled = false;
        //actionData.isChangingAmmoType = false;
#if DEBUG && NotEditor
        Log.Out($"ANIMATOR STATE EXIT : {actionData.invData.item.Name}");
#endif
    }

#if NotEditor
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        //eventBridge.OnReloadUpdate();
        if (actionData == null)
        {
            return;
        }
        if (actionData.isReloadCancelled)
        {
            animator.speed = 30f;

            if (!DoNotForceCancel)
            {
                eventBridge.DelayForceCancelReload(ForceCancelReloadDelay);
            }
#if DEBUG
            Log.Out($"ANIMATOR UPDATE: RELOAD CANCELLED, ANIMATOR SPEED {animator.speed}");
#endif
        }
        if (!actionData.isReloadCancelled && actionData.isReloading)
        {
            actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
            actionData.invData.holdingEntity.FireEvent(MinEventTypes.onReloadUpdate, true);
        }
    }

    private ItemActionRanged.ItemActionDataRanged actionData;
    private EntityPlayerLocal player;
    private AnimationReloadEvents eventBridge;
#endif
}