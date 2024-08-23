using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class AnimationInspectFix : MonoBehaviour
{
    [SerializeField]
    private string inspectName = "Inspect";
    [SerializeField]
    private int layer = 0;
    [SerializeField, Range(0, 1)]
    private float finishTime = 1;
    [SerializeField]
    private bool useStateTag = false;
    private static int inspectHash = Animator.StringToHash("weaponInspect");
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!animator)
        {
            Destroy(this);
        }
    }

    private void Update()
    {
        if (useStateTag)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.IsTag(inspectName) && stateInfo.normalizedTime < finishTime)
            {
                animator.ResetTrigger(inspectHash);
            }
        }
        else
        {
            var transInfo = animator.GetAnimatorTransitionInfo(layer);
            if (transInfo.IsUserName(inspectName) && transInfo.normalizedTime < finishTime)
            {
                animator.ResetTrigger(inspectHash);
            }
        }
    }
}