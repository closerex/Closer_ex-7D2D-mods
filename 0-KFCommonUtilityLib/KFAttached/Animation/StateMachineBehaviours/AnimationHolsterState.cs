using KFCommonUtilityLib;
using UnityEngine;

public class AnimationHolsterState : StateMachineBehaviour
{
    [SerializeField]
    private bool isHolstering = false;
    [SerializeField]
    private string exitTransitionName = "HolsterStateExit";

#if NotEditor
    private EntityPlayerLocal player;
    private ItemModuleTrueHolster.TrueHolsterData holsterData;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (player == null)
        {
            player = animator.GetComponentInParent<EntityPlayerLocal>();
            if (player == null)
            {
                Log.Warning($"AnimationHolsterState: Could not find EntityPlayerLocal. This state will not function correctly.");
                return;
            }
        }
        holsterData = ((isHolstering ? player.inventory?.lastdrawnHoldingItemData : player.inventory?.holdingItemData) as IModuleContainerFor<ItemModuleTrueHolster.TrueHolsterData>)?.Instance;
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
            var info = animator.GetAnimatorTransitionInfo(0);
            if (info.IsUserName(exitTransitionName))
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