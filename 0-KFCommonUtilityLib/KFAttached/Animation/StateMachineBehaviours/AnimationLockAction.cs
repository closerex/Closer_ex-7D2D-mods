#if NotEditor
using KFCommonUtilityLib;
#endif
using UnityEngine;

public class AnimationLockAction : StateMachineBehaviour
{
    public bool lockReload = false;
    public string exitTransitionName = "LockStateExit";
#if NotEditor
    private InventorySlotGurad slotGuard = new InventorySlotGurad();
    private EntityAlive player;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = animator.GetComponentInParent<EntityAlive>();
        if (slotGuard.IsValid(player))
        {
            foreach (var actionData in player.inventory.holdingItemData.actionData)
            {
                if (actionData is IModuleContainerFor<ActionModuleAnimationLocked.AnimationLockedData> lockData)
                {
                    lockData.Instance.isLocked = true;
                    if (lockReload)
                    {
                        lockData.Instance.isReloadLocked = true;
                    }
                }
            }
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!string.IsNullOrEmpty(exitTransitionName))
        {
            var info = animator.GetAnimatorTransitionInfo(0);
            if (info.IsUserName(exitTransitionName))
            {
                if (slotGuard.IsValid(player))
                {
                    foreach (var actionData in player.inventory.holdingItemData.actionData)
                    {
                        if (actionData is IModuleContainerFor<ActionModuleAnimationLocked.AnimationLockedData> lockData)
                        {
                            lockData.Instance.isLocked = false;
                            lockData.Instance.isReloadLocked = false;
                        }
                    }
                }
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (slotGuard.IsValid(player))
        {
            foreach (var actionData in player.inventory.holdingItemData.actionData)
            {
                if (actionData is IModuleContainerFor<ActionModuleAnimationLocked.AnimationLockedData> lockData)
                {
                    lockData.Instance.isLocked = false;
                    lockData.Instance.isReloadLocked = false;
                }
            }
        }
    }
#endif
}
