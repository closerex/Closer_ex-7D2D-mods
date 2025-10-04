using KFCommonUtilityLib;
using UnityEngine;

public class AnimatorBlockingStateMachine : StateMachineBehaviour
{
    public string StartStateTag = "BlockingStart";
    public string ExitStateTag = "BlockingExit";
    public string ExitTransitionTag = "BlockingExitTransition";
#if NotEditor
    private EntityPlayerLocal player;
    private ItemModuleMultiItem.MultiItemInvData multiInvData;
    private ItemActionBlocking.ItemActionBlockingData blockingData;

    public void CheckAction(Animator animator)
    {
        if (!player)
        {
            player = animator.GetLocalPlayerInParent();
            if (player)
            {
                var targets = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
                if (targets)
                {
                    multiInvData = (player.inventory.slots[targets.SlotIndex] as IModuleContainerFor<ItemModuleMultiItem.MultiItemInvData>)?.Instance;
                    if (multiInvData != null)
                    {
                        blockingData = multiInvData.boundInvData?.actionData?[2] as ItemActionBlocking.ItemActionBlockingData;
                    }
                }
            }
        }
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        CheckAction(animator);
        if (blockingData != null)
        {
            if (stateInfo.IsTag(StartStateTag))
            {
                blockingData.isBlockingRunning = true;
                blockingData.isBlockingExited = false;
                blockingData.blockingBeginTime = Time.time;
                var prevData = SetParams();
                player.FireEvent(CustomEnums.onSelfBlockingStart, true);
                RestoreParams(prevData);
                //Log.Out($"Entering blocking state.");
            }
            else if (stateInfo.IsTag(ExitStateTag))
            {
                blockingData.isBlockingRunning = false;
                blockingData.isBlockingExited = false;
                var prevData = SetParams();
                player.FireEvent(CustomEnums.onSelfBlockingStop, true);
                RestoreParams(prevData);
                //Log.Out($"Entering exit state.");
            }
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (blockingData != null && !blockingData.isBlockingExited && stateInfo.IsTag(ExitStateTag) && animator.IsInTransition(layerIndex) && animator.GetAnimatorTransitionInfo(layerIndex).IsUserName(ExitTransitionTag) && !animator.GetNextAnimatorStateInfo(layerIndex).IsTag(StartStateTag))
        {
            blockingData.isBlockingRunning = false;
            blockingData.isBlockingExited = true;
            blockingData.blockingBeginTime = 0f;
            var prevData = SetParams();
            player.FireEvent(CustomEnums.onSelfBlockingExit, true);
            RestoreParams(prevData);
            //Log.Out($"Exiting blocking state from transition.");
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (blockingData != null && !blockingData.isBlockingExited && stateInfo.IsTag(ExitStateTag) && !animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(StartStateTag))
        {
            blockingData.isBlockingRunning = false;
            blockingData.isBlockingExited = true;
            blockingData.blockingBeginTime = 0f;
            var prevData = SetParams();
            player.FireEvent(CustomEnums.onSelfBlockingExit, true);
            RestoreParams(prevData);
            //Log.Out($"Exiting blocking state from exit.");
        }
    }

    private ItemActionData SetParams()
    {
        ItemActionData prevData = player.MinEventContext.ItemActionData;
        multiInvData.SetBoundParams();
        player.MinEventContext.ItemActionData = player.inventory.holdingItemData.actionData[2];
        return prevData;
    }

    private void RestoreParams(ItemActionData prevData)
    {
        multiInvData.RestoreParams(false);
        player.MinEventContext.ItemActionData = prevData;
    }
#endif
}