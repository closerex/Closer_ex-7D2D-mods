using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[AddComponentMenu("")]
public class AnimationGraphBuilder : MonoBehaviour
{
    private Animator animator;
    private RuntimeAnimatorController vanillaRuntimeController;
    private Avatar vanillaAvatar;
    private PlayableGraph graph;
    private AnimationLayerMixerPlayable mixer;
    private AnimatorControllerPlayable weaponControllerPlayable;
    private AnimatorControllerPlayable vanillaControllerPlayable;
    private Avatar weaponAvatar;
    private bool isFpv;
    private int WeaponLayerIndex => isFpv ? 0 : 1;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        vanillaRuntimeController = animator.runtimeAnimatorController;
        vanillaAvatar = animator.avatar;
        isFpv = transform.name == "baseRigFP";
    }

    private void InitGraph()
    {
        if (!graph.IsValid())
        {
            graph = PlayableGraph.Create();
        }

        if (mixer.IsValid())
        {
            mixer.Destroy();
        }
        mixer = AnimationLayerMixerPlayable.Create(graph, isFpv ? 1 : 2, isFpv);
        var output = AnimationPlayableOutput.Create(graph, "output", animator);
        output.SetSourcePlayable(mixer);
        if (!isFpv)
        {
            InitVanilla();
        }
        graph.Play();
    }

    private void InitVanilla()
    {
        if (vanillaControllerPlayable.IsValid())
        {
            vanillaControllerPlayable.Destroy();
        }
        vanillaControllerPlayable = AnimatorControllerPlayable.Create(graph, vanillaRuntimeController);
        mixer.ConnectInput(0, vanillaControllerPlayable, 0, 1.0f);
        mixer.SetLayerAdditive(0, false);
    }

    public void InitWeapon(RuntimeAnimatorController weaponRuntimeController, AvatarMask weaponMask)
    {
        if (!graph.IsValid())
        {
            InitGraph();
        }
        weaponAvatar = AvatarBuilder.BuildGenericAvatar(gameObject, "Origin");
        animator.avatar = weaponAvatar;
        if (weaponControllerPlayable.IsValid())
        {
            weaponControllerPlayable.Destroy();
        }
        weaponControllerPlayable = AnimatorControllerPlayable.Create(graph, weaponRuntimeController);
        int weaponLayerIndex = WeaponLayerIndex;
        mixer.ConnectInput(weaponLayerIndex, weaponControllerPlayable, 0, 1.0f);
        mixer.SetLayerAdditive((uint)weaponLayerIndex, false);
        mixer.SetLayerMaskFromAvatarMask((uint)weaponLayerIndex, weaponMask);
        animator.WriteDefaultValues();
    }

    public void DestroyWeapon()
    {
        mixer.DisconnectInput(isFpv ? 0 : 1);
        if (weaponControllerPlayable.IsValid())
        {
            weaponControllerPlayable.Destroy();
        }
        animator.avatar = vanillaAvatar;
        animator.WriteDefaultValues();
        if (weaponAvatar)
        {
            Destroy(weaponAvatar);
            weaponAvatar = null;
        }
    }

    public void DestroyGraph()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
        if (vanillaControllerPlayable.IsValid())
        {
            vanillaControllerPlayable.Destroy();
        }
        if (weaponControllerPlayable.IsValid())
        {
            weaponControllerPlayable.Destroy();
        }
        if (animator)
        {
            animator.runtimeAnimatorController = vanillaRuntimeController;
            animator.avatar = vanillaAvatar;
            animator.WriteDefaultValues();
        }
    }

    private void OnDisable()
    {
        DestroyGraph();
    }
}
