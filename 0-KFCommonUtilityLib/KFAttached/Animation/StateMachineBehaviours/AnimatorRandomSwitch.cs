using UnityEngine;

[AddComponentMenu("KFAttachments/Utils/Animator Random Switch")]
public class AnimatorRandomSwitch : StateMachineBehaviour
{
    [SerializeField]
    private string parameter;
    [SerializeField]
    private int stateCount;

    private int[] stateHits;
    int totalHits;

    private void Awake()
    {
        stateHits = new int[stateCount];
        for (int i = 0; i < stateCount; i++)
        {
            stateHits[i] = 1;
        }
        totalHits = stateCount;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int rand = Random.Range(0, totalHits);
        int cur = 0;
        bool found = false;
        for (int i = 0; i < stateHits.Length; i++)
        {
            cur += stateHits[i];
            if (cur > rand && !found)
            {
                animator.SetInteger(parameter, i);
                found = true;
                stateHits[i] = 1;
            }
            else
            {
                stateHits[i] = 2;
            }
        }
        totalHits = stateCount * 2 - 1;
    }
}