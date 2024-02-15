using UnityEngine;

public class AnimatorActionIndexDebug : StateMachineBehaviour
{
    public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
    {
        Log.Out($"StateMachine enter, Animator action index: {animator.GetInteger("ExecutingActionIndex")}");
    }

    public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
    {
        Log.Out($"StateMachine exit, Animator action index: {animator.GetInteger("ExecutingActionIndex")}");
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Log.Out($"State entered!");
    }

    public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("WeaponFire"))
        {
            Log.Out($"OnStateMove: Fire trigger set, Animator action index: {animator.GetInteger("ExecutingActionIndex")}");
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("WeaponFire"))
        {
            Log.Out($"OnStateUpdate: Fire trigger set, Animator action index: {animator.GetInteger("ExecutingActionIndex")}");
        }
    }
}