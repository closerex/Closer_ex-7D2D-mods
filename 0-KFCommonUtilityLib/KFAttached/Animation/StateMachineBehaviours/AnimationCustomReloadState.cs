using UnityEngine;

public class AnimationCustomReloadState : StateMachineBehaviour
{
    [SerializeField]
    private int actionIndex = 0;
#if NotEditor

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.speed = 1f;
        animator.SetBool("Reload", false);
        if (actionData == null || player == null || eventBridge == null)
        {
            player = animator.GetComponentInParent<EntityPlayerLocal>();
            actionData = player.inventory.holdingItemData.actionData[actionIndex] as ItemActionRanged.ItemActionDataRanged;
            eventBridge = animator.GetComponent<AnimationReloadEvents>();
        }

#if DEBUG
        Log.Out($"ANIMATOR STATE ENTER : {actionData.invData.item.Name}");
#endif
        eventBridge.OnReloadStart(actionIndex);
        eventBridge.OnReloadUpdate();
    }

    // Token: 0x06000B5C RID: 2908 RVA: 0x000AC3A8 File Offset: 0x000AA5A8
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.speed = 1f;
        eventBridge.OnReloadUpdate();
        if (actionData == null)
        {
            return;
        }
        actionData.isReloading = false;
        actionData.isReloadCancelled = false;
        actionData.isChangingAmmoType = false;
#if DEBUG
        Log.Out($"ANIMATOR STATE EXIT : {actionData.invData.item.Name}");
#endif
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        eventBridge.OnReloadUpdate();
        if (actionData == null)
        {
            return;
        }
        if (actionData.isReloadCancelled)
        {
            animator.speed = 30f;
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