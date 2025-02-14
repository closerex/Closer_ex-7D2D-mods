#if NotEditor
using KFCommonUtilityLib;
using KFCommonUtilityLib.Scripts.StaticManagers;
#endif
using UnityEngine;

public class AnimationLockAction : StateMachineBehaviour
{
    public bool lockReload = false;
#if NotEditor
    private InventorySlotGurad slotGuard = new InventorySlotGurad();
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<EntityAlive>();
        if (slotGuard.IsValid(player))
        {
            if (player.inventory.holdingItemData.actionData[MultiActionManager.GetActionIndexForEntity(player)] is IModuleContainerFor<ActionModuleAnimationLocked.AnimationLockedData> lockData)
            {
                lockData.Instance.isLocked = true;
                if (lockReload)
                {
                    lockData.Instance.isReloadLocked = true;
                }
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var player = animator.GetComponentInParent<EntityAlive>();
        if (slotGuard.IsValid(player))
        {
            if (player.inventory.holdingItemData.actionData[MultiActionManager.GetActionIndexForEntity(player)] is IModuleContainerFor<ActionModuleAnimationLocked.AnimationLockedData> lockData)
            {
                lockData.Instance.isLocked = false;
                lockData.Instance.isReloadLocked = false;
            }
        }
    }
#endif
}
