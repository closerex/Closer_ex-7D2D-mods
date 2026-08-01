using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

[AddComponentMenu("")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(int.MaxValue)]
internal class ItemAnimatorUpdate : MonoBehaviour
{
    private Animator animator;
    private RuntimeAnimatorController animatorController;
    private RigBuilder weaponRB;
    private PlayableGraph graph;
    private AnimatorControllerPlayable mainPlayable;
    public AnimatorControllerPlayable AnimatorControllerPlayable => mainPlayable;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animatorController = animator.runtimeAnimatorController;
        animator.runtimeAnimatorController = null;
        if (TryGetComponent(out weaponRB) && weaponRB.enabled)
        {
            weaponRB.enabled = false;
        }
    }

    private void OnEnable()
    {
        graph = PlayableGraph.Create("ItemAnimatorUpdate");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        
        mainPlayable = AnimatorControllerPlayable.Create(graph, animatorController);
        var mainOutput = AnimationPlayableOutput.Create(graph, "MainOutput", animator);
        mainOutput.SetSourcePlayable(mainPlayable);

        if (weaponRB)
        {
            weaponRB.Build(graph);
        }
        Update(0f);
    }

    private void OnDisable()
    {
        if (graph.IsValid())
            graph.Destroy();
        mainPlayable = default;
    }

    private void Update()
    {
        Update(Time.deltaTime);
    }

    private void Update(float _dt)
    {
        if (graph.IsValid())
        {
            if (weaponRB)
            {
                weaponRB.SyncLayers();
            }
            graph.Evaluate(_dt);
        }
    }
}