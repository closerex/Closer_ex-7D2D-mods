using System;
using UnityEngine;

public class AnimationInspectFix : MonoBehaviour, IPlayableGraphRelated
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
    private IAnimatorWrapper wrapper;

    private void Awake()
    {
    }

    private void Update()
    {
        if (wrapper == null || !wrapper.IsValid)
        {
            var animator = GetComponent<Animator>();
            if (!animator)
            {
                Destroy(this);
                return;
            }
            wrapper = animator.GetItemAnimatorWrapper();
        }
        if (useStateTag)
        {
            var stateInfo = wrapper.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.IsTag(inspectName) && stateInfo.normalizedTime < finishTime)
            {
                wrapper.ResetTrigger(inspectHash);
            }
        }
        else
        {
            var transInfo = wrapper.GetAnimatorTransitionInfo(layer);
            if (transInfo.IsUserName(inspectName) && transInfo.normalizedTime < finishTime)
            {
                wrapper.ResetTrigger(inspectHash);
            }
        }
    }

    public MonoBehaviour Init(Transform playerAnimatorTrans, bool isLocalPlayer)
    {
        enabled = false;
        var copy = isLocalPlayer ? playerAnimatorTrans.AddMissingComponent<AnimationInspectFix>() : null;
        if (copy)
        {
            copy.enabled = true;
        }
        return copy;
    }

    public void Disable(Transform playerAnimatorTrans)
    {
        enabled = false;
    }
}