#if NotEditor
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[AddComponentMenu("")]
public class AnimationGraphBuilder : MonoBehaviour
{
    public enum ParamInWrapper
    {
        None,
        Vanilla,
        Weapon,
        Attachments,
        Both
    }
    private bool inited = false;
    private Animator animator;
    private RuntimeAnimatorController vanillaRuntimeController;
    private Avatar vanillaAvatar;
    private PlayableGraph graph;
    private AnimationLayerMixerPlayable mixer;
    private AnimatorControllerPlayable weaponControllerPlayable;
    private AnimatorControllerPlayable vanillaControllerPlayable;
    private Avatar weaponAvatar;
    private AvatarMask weaponMask;
    private bool isFpv;
    private bool isLocalPlayer = true;
    private readonly List<MonoBehaviour> graphRelatedBehaviours = new List<MonoBehaviour>();
    private AnimatorControllerParameter[] parameters;
    private readonly Dictionary<int, ParamInWrapper> paramMapping = new Dictionary<int, ParamInWrapper>();
    private readonly Dictionary<string, ParamInWrapper> paramMappingDebug = new Dictionary<string, ParamInWrapper>();

    //only set in Unity Explorer
    private static bool debugLog = false;
    //private Animator[] childAnimators = Array.Empty<Animator>();

    public bool HasWeaponOverride => graph.IsValid();
    //public AnimatorControllerPlayable VanillaPlayable => vanillaControllerPlayable;
    //public AnimatorControllerPlayable WeaponPlayable => weaponControllerPlayable;
    public AnimatorControllerParameter[] Parameters => parameters;
    public AnimationTargetsAbs CurrentTarget { get; private set; }
    public IAnimatorWrapper VanillaWrapper { get; private set; }
    public IAnimatorWrapper WeaponWrapper { get; private set; }
    public AttachmentWrapper AttachmentWrapper { get; private set; }
    public static IAnimatorWrapper DummyWrapper { get; } = new AnimatorWrapper(null);
#if NotEditor
    public EntityPlayer Player { get; private set; }
#endif

    public void Init()
    {
        if (inited)
        {
            return;
        }
        isFpv = transform.name == "baseRigFP";
        animator = GetComponent<Animator>();
        animator.logWarnings = false;
        VanillaWrapper = new AnimatorWrapper(animator);
        WeaponWrapper = new AnimatorWrapper(null);
        parameters = animator.parameters;
        UpdateParamMapping();
        vanillaAvatar = animator.avatar;
#if NotEditor
        vanillaRuntimeController = isFpv ? SDCSUtils.FPAnimController : SDCSUtils.TPAnimController;
        isLocalPlayer = (Player = GetComponentInParent<EntityPlayer>(true)) is EntityPlayerLocal;
#else
        vanillaRuntimeController = animator.runtimeAnimatorController;
#endif
        inited = true;
    }

    private void UpdateParamMapping()
    {
        paramMapping.Clear();
        paramMappingDebug.Clear();
        var paramList = new List<AnimatorControllerParameter>();
        paramList.AddRange(animator.parameters);
        for (int i = 0; i < VanillaWrapper.GetParameterCount(); i++)
        {
            paramMapping.Add(VanillaWrapper.GetParameter(i).nameHash, ParamInWrapper.Vanilla);
            paramMappingDebug.Add(VanillaWrapper.GetParameter(i).name, ParamInWrapper.Vanilla);
        }
        if (WeaponWrapper != null && WeaponWrapper.IsValid)
        {
            for (int i = 0; i < WeaponWrapper.GetParameterCount(); i++)
            {
                var param = WeaponWrapper.GetParameter(i);
                if (paramMapping.ContainsKey(param.nameHash))
                {
                    paramMapping[param.nameHash] = ParamInWrapper.Both;
                    paramMappingDebug[param.name] = ParamInWrapper.Both;
                }
                else
                {
                    paramMapping.Add(param.nameHash, ParamInWrapper.Weapon);
                    paramMappingDebug.Add(param.name, ParamInWrapper.Weapon);
                    paramList.Add(param);
                }
            }
        }
        if (AttachmentWrapper != null && AttachmentWrapper.IsValid)
        {
            foreach (var animator in AttachmentWrapper.animators)
            {
                for (int i = 0; i < animator.parameterCount; i++)
                {
                    var param = animator.GetParameter(i);
                    if (!paramMapping.ContainsKey(param.nameHash))
                    {
                        paramMapping[param.nameHash] = ParamInWrapper.Attachments;
                        paramList.Add(param);
                    }
                }
            }
        }
        parameters = paramList.ToArray();
    }

    public ParamInWrapper GetWrapperRoleByParamHash(int nameHash)
    {
        if (paramMapping.TryGetValue(nameHash, out var role))
            return role;
        return ParamInWrapper.None;
    }

    public ParamInWrapper GetWrapperRoleByParamName(string name)
    {
        if (string.IsNullOrEmpty(name) || !paramMapping.TryGetValue(Animator.StringToHash(name), out var role))
            return ParamInWrapper.None;
        return role;
    }

    public ParamInWrapper GetWrapperRoleByParam(AnimatorControllerParameter param)
    {
        if (param == null || !paramMapping.TryGetValue(param.nameHash, out var role))
            return ParamInWrapper.None;
        return role;
    }

    public void SetChildFloat(int nameHash, float value)
    {
        if (AttachmentWrapper != null && AttachmentWrapper.IsValid)
        {
            AttachmentWrapper.SetFloat(nameHash, value);
        }
    }

    public void SetChildBool(int nameHash, bool value)
    {
        if (AttachmentWrapper != null && AttachmentWrapper.IsValid)
        {
            AttachmentWrapper.SetBool(nameHash, value);
        }
    }

    public void SetChildInteger(int nameHash, int value)
    {
        if (AttachmentWrapper != null && AttachmentWrapper.IsValid)
        {
            AttachmentWrapper.SetInteger(nameHash, value);
        }
    }

    public void SetChildTrigger(int nameHash)
    {
        if (AttachmentWrapper != null && AttachmentWrapper.IsValid)
        {
            AttachmentWrapper.SetTrigger(nameHash);
        }
    }

    public void ResetChildTrigger(int nameHash)
    {
        if (AttachmentWrapper != null && AttachmentWrapper.IsValid)
        {
            AttachmentWrapper.ResetTrigger(nameHash);
        }
    }

    private void InitGraph()
    {
        animator.runtimeAnimatorController = null;
        if (graph.IsValid())
        {
            return;
        }

        graph = PlayableGraph.Create();
        mixer = AnimationLayerMixerPlayable.Create(graph, 2);
        var output = AnimationPlayableOutput.Create(graph, "output", animator);
        output.SetSourcePlayable(mixer);
        InitVanilla();
        graph.Play();
    }

    private void InitVanilla()
    {
        vanillaControllerPlayable = AnimatorControllerPlayable.Create(graph, vanillaRuntimeController);
        mixer.ConnectInput(0, vanillaControllerPlayable, 0, isFpv ? 0 : 1.0f);
        mixer.SetLayerAdditive(0, false);
    }

    public void InitWeapon(Transform weaponRoot, RuntimeAnimatorController weaponRuntimeController, AvatarMask weaponMask)
    {
        InitGraph();
        InitBehaviours(weaponRoot);
        weaponAvatar = AvatarBuilder.BuildGenericAvatar(gameObject, "Origin");
        animator.avatar = weaponAvatar;
        weaponControllerPlayable = AnimatorControllerPlayable.Create(graph, weaponRuntimeController);
        mixer.ConnectInput(1, weaponControllerPlayable, 0, 1.0f);
        mixer.SetLayerAdditive(1, false);
        if (weaponMask)
        {
            mixer.SetLayerMaskFromAvatarMask(1, weaponMask);
        }
        this.weaponMask = weaponMask;
    }

    private void DestroyGraph()
    {
        CleanupBehaviours();
        weaponMask = null;
        if (graph.IsValid())
        {
            graph.Destroy();
        }
        if (animator)
        {
            animator.runtimeAnimatorController = vanillaRuntimeController;
            animator.avatar = vanillaAvatar;
        }
    }

    private void DestroyWeapon()
    {
        CleanupBehaviours();
        Destroy(weaponMask);
        weaponMask = null;
        if (weaponControllerPlayable.IsValid())
        {
            mixer.DisconnectInput(1);
            weaponControllerPlayable.Destroy();
        }
        animator.avatar = vanillaAvatar;
        Destroy(weaponAvatar);
        weaponAvatar = null;
        animator.runtimeAnimatorController = vanillaRuntimeController;
    }

    public void SetCurrentTarget(AnimationTargetsAbs target)
    {
        if (CurrentTarget == target)
        {
            UpdateChildAnimatorArray(target);
            return;
        }

        var sw = new Stopwatch();
        sw.Start();
#if NotEditor
        bool wasCrouching = VanillaWrapper != null && VanillaWrapper.IsValid && VanillaWrapper.GetBool(AvatarController.isCrouchingHash);
#endif
        //var rb = animator.transform.AddMissingComponent<RigBuilder>();
        //rb.enabled = false;
        bool curTargetValid = CurrentTarget && CurrentTarget.IsAnimationSet;
        if (CurrentTarget)
        {
            CurrentTarget.IsCurrent = false;
        }
        if (curTargetValid)
        {
            CurrentTarget.SetEnabled(false);
        }

        bool didUseGraph = curTargetValid && CurrentTarget.UseGraph;
        bool willUseGraph = target && target.UseGraph;
        bool nextTargetValid = target && target.ItemCurrent;
        if (HasWeaponOverride)
        {
            if (willUseGraph)
            {
                DestroyWeapon();
            }
            else
            {
                DestroyGraph();
            }
        }

        CurrentTarget = target;
        if (target)
        {
            target.IsCurrent = true;
        }
        if (nextTargetValid)
        {
            target.SetEnabled(true);
        }
        //        Log.Out($"\n#=================Rebuild Start");
        //#if NotEditor
        //        Log.Out($"Remaining RigLayers on build:\n{string.Join("\n", rb.layers.Select(layer => layer.name))}");
        //#endif
        //        animator.UnbindAllSceneHandles();
        //        animator.UnbindAllStreamHandles();
        //        rb.enabled = true;
        //        animator.Rebind();
        //        Log.Out($"#=================Rebuild Finish\n");
        if (curTargetValid || nextTargetValid)
        {
            animator.WriteDefaultValues();
        }

        if (willUseGraph)
        {
            VanillaWrapper = new PlayableWrapper(vanillaControllerPlayable);
            WeaponWrapper = new PlayableWrapper(weaponControllerPlayable);
        }
        else
        {
            VanillaWrapper = new AnimatorWrapper(animator);
            if (nextTargetValid)
            {
                WeaponWrapper = new AnimatorWrapper(target.ItemAnimator);
            }
            else
            {
                WeaponWrapper = DummyWrapper;
            }
        }

        UpdateChildAnimatorArray(target);
        UpdateParamMapping();
#if NotEditor
        if (curTargetValid || nextTargetValid)
        {
            animator.SetWrappedBool(AvatarController.isCrouchingHash, wasCrouching);
            if (isFpv)
            {
                VanillaWrapper.Play("idle", 0, 0f);
                VanillaWrapper.SetInteger(AvatarController.weaponHoldTypeHash, -1);
            }
            if (wasCrouching && didUseGraph != willUseGraph && !isFpv && VanillaWrapper.GetLayerCount() > 4)
            {
                VanillaWrapper.Play("2HGeneric", 4, 0);
            }
        }

        //if (isFpv)
        //{
        //    if (!nextTargetValid)
        //    {
        //        VanillaWrapper.SetLayerWeight(1, 1f);
        //    }
        //    else
        //    {
        //        VanillaWrapper.SetLayerWeight(1, 0f);
        //    }
        //}

#endif
        sw.Stop();
        Log.Out($"changing animation target to {(target ? target.name : "null")} took {sw.ElapsedMilliseconds}");
        if (debugLog)
        {
            Log.Out(StackTraceUtility.ExtractStackTrace());
        }
    }

    private void UpdateChildAnimatorArray(AnimationTargetsAbs target)
    {
        Animator[] childAnimators;
        if (target && target.ItemCurrentOrDefault)
        {
            List<Animator> animators = new List<Animator>();
            foreach (Transform trans in target.ItemCurrentOrDefault)
            {
                animators.AddRange(trans.GetComponentsInChildren<Animator>());
            }
            if (target.ItemAnimator)
            {
                animators.Remove(target.ItemAnimator);
            }
            else if (!target.ItemCurrent && target.TryGetComponent<Animator>(out Animator vanillaAnimator))
            {
                animators.Remove(vanillaAnimator);
            }
            childAnimators = animators.ToArray();
        }
        else
        {
            childAnimators = Array.Empty<Animator>();
        }
        AttachmentWrapper = new AttachmentWrapper(childAnimators);
    }

    private void InitBehaviours(Transform weaponRoot)
    {
        foreach (var scripts in weaponRoot.GetComponents<IPlayableGraphRelated>())
        {
            var behaviour = scripts.Init(transform, isLocalPlayer);
            if (behaviour)
            {
                graphRelatedBehaviours.Add(behaviour);
            }
        }
    }

    private void CleanupBehaviours()
    {
        foreach (var behaviour in graphRelatedBehaviours)
        {
            if (behaviour)
            {
                (behaviour as IPlayableGraphRelated)?.Disable(transform);
            }
        }
        graphRelatedBehaviours.Clear();
    }

    private void OnDisable()
    {
        SetCurrentTarget(null);
    }
}
