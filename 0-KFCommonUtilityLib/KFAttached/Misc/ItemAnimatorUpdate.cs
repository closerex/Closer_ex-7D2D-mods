using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

[AddComponentMenu("")]
[DisallowMultipleComponent]
internal class ItemAnimatorUpdate : MonoBehaviour
{
    private Animator animator;
    private RigBuilder weaponRB;
    internal PlayableGraph graph;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        TryGetComponent<RigBuilder>(out weaponRB);
    }

    private void Update()
    {
        //animator.playableGraph.Evaluate(Time.deltaTime);
        animator.Update(Time.deltaTime);
        //graph.Evaluate(Time.deltaTime);
        //weaponRB?.Evaluate(Time.deltaTime);
    }
}