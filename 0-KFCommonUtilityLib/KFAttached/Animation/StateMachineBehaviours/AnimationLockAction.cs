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
        if (!targets)
            return;
        foreach (var actionData in player.inventory.slots[targets.SlotIndex].actionData)
        {
            if (!actionData.IsModuleContainerFor(out ActionModuleAnimationLocked.AnimationLockedData lockData))
                continue;
            lockData.isLocked = true;
            if (lockReload)
            {
                lockData.isReloadLocked = true;
            }
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (string.IsNullOrEmpty(exitTransitionName))
            return;
        var info = animator.GetAnimatorTransitionInfo(0);
        
        if (!info.IsUserName(exitTransitionName))
            return;
        
        if (!targets)
            return;
        
        foreach (var actionData in player.inventory.slots[targets.SlotIndex].actionData)
        {
            if (!actionData.IsModuleContainerFor(out ActionModuleAnimationLocked.AnimationLockedData lockData))
                continue;
            lockData.isLocked = false;
            lockData.isReloadLocked = false;
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!targets)
            return;
        
        foreach (var actionData in player.inventory.slots[targets.SlotIndex].actionData)
        {
            if (!actionData.IsModuleContainerFor(out ActionModuleAnimationLocked.AnimationLockedData lockData))
                continue;
            lockData.isLocked = false;
            lockData.isReloadLocked = false;
        }
    }
#endif
}
