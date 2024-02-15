using UnityEngine;

public class AnimationAimRecoilResetState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<AnimationAimRecoilReferences>()?.Rollback();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<AnimationAimRecoilReferences>()?.Rollback();
    }
}
