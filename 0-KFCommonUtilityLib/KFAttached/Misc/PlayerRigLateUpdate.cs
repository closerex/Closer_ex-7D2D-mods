using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerRigLateUpdate : MonoBehaviour
{
    private Animator animator;
    private RigBuilder rigBuilder;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        rigBuilder = GetComponent<RigBuilder>();
    }

    private void OnAnimatorMove()
    {
        if (rigBuilder.enabled)
            ForceToManualUpdate();
    }

    private void LateUpdate()
    {
        rigBuilder.Evaluate(Time.deltaTime);
    }

    private void ForceToManualUpdate()
    {
        RigTargets.RebuildRig(animator, rigBuilder);
    }
}
