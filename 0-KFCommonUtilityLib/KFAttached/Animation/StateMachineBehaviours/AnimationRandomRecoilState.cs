using UnityEngine;

public class AnimationRandomRecoilState : StateMachineBehaviour
{
    [SerializeField] private Vector3 positionMultiplier = Vector3.one;
    [SerializeField] private Vector3 rotationMultiplier = Vector3.one;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<AnimationProceduralRecoildAbs>()?.AddRecoil(positionMultiplier, rotationMultiplier);
    }
}
