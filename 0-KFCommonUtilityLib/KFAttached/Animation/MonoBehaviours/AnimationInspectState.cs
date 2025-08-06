using UnityEngine;

public class AnimationInspectState : StateMachineBehaviour
{
    private static readonly int InspectingTrigger = Animator.StringToHash("weaponInspect");
    private static readonly int InspectingHash = Animator.StringToHash("IsInspecting");
    [SerializeField]
    private string inspectName = "Inspect";
    [SerializeField, Range(0, 1)]
    private float finishTime = 1;
    [SerializeField]
    private bool useStateTag = true;
    private IAnimatorWrapper wrapper;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (wrapper == null || !wrapper.IsValid)
        {
            wrapper = animator.GetItemAnimatorWrapper();
        }
        wrapper.SetBool(InspectingHash, true);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        bool isInspecting = false;
        if (useStateTag)
        {
            if (stateInfo.IsTag(inspectName) && stateInfo.normalizedTime < finishTime)
            {
                isInspecting = true;
            }
        }
        else
        {
            var transInfo = wrapper.GetAnimatorTransitionInfo(layerIndex);
            if (transInfo.IsUserName(inspectName) && transInfo.normalizedTime < finishTime)
            {
                isInspecting = true;
            }
        }
        if (isInspecting)
        {
            wrapper.ResetTrigger(InspectingTrigger);
            wrapper.SetBool(InspectingHash, true);
        }
        else
        {
            wrapper.SetBool(InspectingHash, false);
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        wrapper.SetBool(InspectingHash, false);
    }
}