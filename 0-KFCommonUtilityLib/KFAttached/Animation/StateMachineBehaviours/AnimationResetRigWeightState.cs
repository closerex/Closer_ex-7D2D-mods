using UnityEngine;

public class AnimationResetRigWeightState : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<RigWeightOverTime>()?.SetWeight(0);
    }
}
