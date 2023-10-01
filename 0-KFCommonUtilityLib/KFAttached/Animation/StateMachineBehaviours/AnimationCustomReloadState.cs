using UnityEngine;

public class AnimationCustomReloadState : StateMachineBehaviour
{
#if !UNITY_EDITOR

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool("Reload", false);
        EntityAlive componentInParent = animator.GetComponentInParent<EntityAlive>();
        if (componentInParent == null)
        {
            return;
        }
        actionData = componentInParent.inventory.holdingItemData.actionData[0] as ItemActionRanged.ItemActionDataRanged;
        //Log.Out("ANIMATOR STATE ENTER");
        animator.GetComponent<AnimationReloadEvents>()?.OnReloadStart();
        stateEnteredThisFrame = true;
    }

    // Token: 0x06000B5C RID: 2908 RVA: 0x000AC3A8 File Offset: 0x000AA5A8
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.speed = 1f;
        if (actionData == null)
        {
            return;
        }
        //actionData.isReloading = false;
        //actionData.isReloadCancelled = false;
        //actionData.isChangingAmmoType = false;
        //Log.Out("ANIMATOR STATE EXIT");
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        stateEnteredThisFrame = false;
        if (actionData == null)
        {
            return;
        }
        if (actionData.isReloadCancelled)
        {
            animator.speed = 30f;
            //Log.Out($"ANIMATOR UPDATE: RELOAD CANCELLED, ANIMATOR SPEED {animator.speed}");
        }
        if (!actionData.isReloadCancelled && actionData.isReloading)
        {
            actionData.invData.holdingEntity.MinEventContext.ItemActionData = actionData;
            actionData.invData.holdingEntity.FireEvent(MinEventTypes.onReloadUpdate, true);
        }
    }

    private ItemActionRanged.ItemActionDataRanged actionData;
    private static bool stateEnteredThisFrame = false;
#endif
}