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
    private string inspectTransName = "Inspect";
    [SerializeField]
    private int layer = 0;
    [SerializeField, Range(0, 1)]
    private float finishTime = 1;
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
        var info = animator.GetAnimatorTransitionInfo(layer);
        if (info.IsUserName(inspectTransName) && info.normalizedTime < finishTime)
        {
            animator.ResetTrigger(inspectHash);
        }
    }

    //private void LateUpdate()
    //{
    //    Rig rig = GetComponentInChildren<Rig>(true);
    //    if (rig)
    //    {
    //        if (rig.weight > 0)
    //        {
    //            rig.weight -= 0.01f;
    //        }
    //        if (rig.weight <= 0)
    //        {
    //            rig.weight = 1;
    //        }
    //    }
    //}
}