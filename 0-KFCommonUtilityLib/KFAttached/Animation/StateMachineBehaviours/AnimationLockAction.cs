#if NotEditor
using System.Collections.Generic;
using KFCommonUtilityLib;
#endif
using UnityEngine;

public class AnimationLockAction : StateMachineBehaviour
{
    public bool lockReload = false;
    public string exitTransitionName = "LockStateExit";
#if NotEditor
    private readonly List<ActionModuleAnimationLocked.AnimationLockedData> lockedData = new();

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        UnlockActions();
        var player = animator.GetComponentInParent<EntityAlive>();
        var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
        if (!targets || targets.InventoryData == null)
            return;
        foreach (var actionData in targets.InventoryData.actionData)
        {
            if (!actionData.IsModuleContainerFor(out ActionModuleAnimationLocked.AnimationLockedData lockData))
                continue;
            lockedData.Add(lockData);
            lockData.isLocked = true;
            if (lockReload)
                lockData.isReloadLocked = true;
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!string.IsNullOrEmpty(exitTransitionName) && animator.GetAnimatorTransitionInfo(0).IsUserName(exitTransitionName))
            UnlockActions();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        UnlockActions();
    }

    private void UnlockActions()
    {
        foreach (var lockData in lockedData)
        {
            lockData.isLocked = false;
            lockData.isReloadLocked = false;
        }
        lockedData.Clear();
    }
#endif
}
