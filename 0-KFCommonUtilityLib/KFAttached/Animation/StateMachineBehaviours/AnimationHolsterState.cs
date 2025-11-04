using KFCommonUtilityLib;
using UnityEngine;
#if NotEditor
#endif

public class AnimationHolsterState : StateMachineBehaviour
{
    [SerializeField]
    public bool isHolstering = false;
    [SerializeField]
    public string exitTransitionName = "HolsterStateExit";

#if NotEditor
    private EntityPlayerLocal player;
    private ItemModuleTrueHolster.TrueHolsterData holsterData;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (player == null)
        {
            player = animator.GetLocalPlayerInParent();
            if (player == null)
            {
                Log.Warning($"AnimationHolsterState: Could not find EntityPlayerLocal. This state will not function correctly.");
                return;
            }
        }
        //holsterData = ((isHolstering ? player.inventory?.lastdrawnHoldingItemData : player.inventory?.holdingItemData) as IModuleContainerFor<ItemModuleTrueHolster.TrueHolsterData>)?.Instance;
        var activeRig = AnimationRiggingManager.GetActiveRigTargetsFromPlayer(player);
        if (activeRig)
        {
            holsterData = (player.inventory?.GetItemDataInSlot(activeRig.SlotIndex) as IModuleContainerFor<ItemModuleTrueHolster.TrueHolsterData>)?.Instance;
        }
        if (holsterData != null)
        {
            if (isHolstering && holsterData.module.holsterDelayActive)
            {
                holsterData.IsHolstering = true;
            }
            else if (!isHolstering && holsterData.module.unholsterDelayActive)
            {
                holsterData.IsUnholstering = true;
            }
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (holsterData != null && !string.IsNullOrEmpty(exitTransitionName))
        {
            var info = animator.GetAnimatorTransitionInfo(layerIndex);
            if (info.IsUserName(exitTransitionName))
            {
                if (holsterData != null)
                {
                    if (isHolstering)
                    {
                        if (holsterData.IsHolstering)
                            holsterData.IsHolstering = false;
                    }
                    else
                    {
                        if (holsterData.IsUnholstering)
                            holsterData.IsUnholstering = false;
                    }
                }
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (holsterData != null)
        {
            if (isHolstering)
            {
                holsterData.IsHolstering = false;
            }
            else
            {
                holsterData.IsUnholstering = false;
            }
        }
    }
#endif
}