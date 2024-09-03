using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[Obsolete]
[AddComponentMenu("")]
public class PlayerRigLateUpdate : MonoBehaviour
{
#if NotEditor
    //private Animator animator;
    //private RigBuilder rigBuilder;
    //private void Awake()
    //{
    //    animator = GetComponent<Animator>();
    //    rigBuilder = GetComponent<RigBuilder>();
    //}

    //private void OnAnimatorMove()
    //{
    //    if (rigBuilder.enabled)
    //        ForceToManualUpdate();
    //}

    //private void LateUpdate()
    //{
    //    rigBuilder.Evaluate(Time.deltaTime);
    //}

    //private void ForceToManualUpdate()
    //{
    //    RigTargets.RebuildRig(animator, rigBuilder);
    //}
#endif
}
