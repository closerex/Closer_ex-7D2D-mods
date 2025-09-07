#if NotEditor
using KFCommonUtilityLib;
#endif
using UnityEngine;

public class AnimationLockAction : StateMachineBehaviour
{
    public bool lockReload = false;
    public string exitTransitionName = "LockStateExit";
#if NotEditor
    private AnimationTargetsAbs targets;
    private EntityAlive player;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        player = animator.GetComponentInParent<EntityAlive>();
        targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
        if (targets)
        {
            foreach (var actionData in player.inventory.slots[targets.SlotIndex].actionData)
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
                if (targets)
                {
                    foreach (var actionData in player.inventory.slots[targets.SlotIndex].actionData)
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
        if (targets)
        {
            foreach (var actionData in player.inventory.slots[targets.SlotIndex].actionData)
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
