using UnityEngine;

[AddComponentMenu("KFAttachments/Utils/Animator Random Switch")]
public class AnimatorRandomSwitch : StateMachineBehaviour
{
    [SerializeField]
    private string parameter;
    [SerializeField]
    private int stateCount;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetInteger(parameter, Random.Range(0, stateCount));
    }
}