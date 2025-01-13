using KFCommonUtilityLib.Scripts.StaticManagers;
using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

public class PlayGraphTargets : AnimationTargetsAbs
{
    [SerializeField]
    public Transform itemFpv;
    [SerializeField]
    public Transform attachmentReference;
    [SerializeField]
    private RuntimeAnimatorController weaponRuntimeController;

    private Rig rigFpv;
    private RigLayer rigLayer;
    private Animator itemAnimatorFpv;
    public override Transform ItemFpv { get => itemFpv; protected set => itemFpv = value; }

    public override Transform AttachmentRef { get => attachmentReference; protected set => attachmentReference = value; }

    protected override Animator ItemAnimatorFpv => itemAnimatorFpv;

    protected override void Awake()
    {
        base.Awake();
        if (!itemFpv)
        {
            return;
        }

        rigFpv = itemFpv.GetComponentInChildren<Rig>();
#if NotEditor
        rigFpv.gameObject.name += $"_UID_{TypeBasedUID<AnimationTargetsAbs>.UID}";
        AnimationRiggingManager.AddRigExcludeName(rigFpv.gameObject.name);

        itemFpv.gameObject.SetActive(false);
#endif
    }

    protected override void Init()
    {
        if (Destroyed)
        {
            return;
        }
        if (IsFpv)
        {
            itemAnimatorFpv = PlayerAnimatorTrans.GetComponent<Animator>();
            itemFpv.SetParent(itemAnimatorFpv.avatarRoot);
            itemFpv.position = Vector3.zero;
            itemFpv.localPosition = Vector3.zero;
            itemFpv.localRotation = Quaternion.identity;
            var rc = rigFpv.GetComponent<RigConverter>();
            rc.targetRoot = PlayerAnimatorTrans;
            rc.Rebind();
        }
        else
        {
            itemAnimatorFpv = null;
            itemFpv.SetParent(PlayerAnimatorTrans.parent);
            itemFpv.position = Vector3.zero;
            itemFpv.localPosition = Vector3.zero;
            itemFpv.localRotation = Quaternion.identity;
        }
    }

    protected override void SetupFpv()
    {
        if (!PlayerAnimatorTrans || !itemFpv)
        {
            Destroy();
            return;
        }

        Stopwatch sw = new Stopwatch();
        sw.Start();

        var builder = PlayerAnimatorTrans.AddMissingComponent<AnimationGraphBuilder>();
        builder.InitWeapon(weaponRuntimeController, null);
        //copy scripts to player

        if (rigFpv)
        {
            var animator = PlayerAnimatorTrans.GetComponent<Animator>();
            animator.UnbindAllStreamHandles();
            animator.UnbindAllSceneHandles();

            var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
            rigBuilder.layers.RemoveAll(r => r.rig == rigFpv);
            rigLayer = new RigLayer(rigFpv, true);
            rigBuilder.layers.Add(rigLayer);
            animator.Rebind();
            rigBuilder.Build();
        }

        sw.Stop();
        string info = $"setup animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);
    }

    protected override void RemoveFpv()
    {
        if (!PlayerAnimatorTrans || !itemFpv)
        {
            Destroy();
            return;
        }
        Stopwatch sw = new Stopwatch();
        sw.Start();

        var builder = PlayerAnimatorTrans.AddMissingComponent<AnimationGraphBuilder>();
        builder.DestroyWeapon();
        //remove scripts from player?

        if (rigFpv)
        {
            var animator = PlayerAnimatorTrans.GetComponent<Animator>();
            var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
            rigBuilder.layers.Remove(rigLayer);
            animator.UnbindAllStreamHandles();
            animator.UnbindAllSceneHandles();

            rigFpv.transform.SetParent(transform, false);
            animator.Rebind();
            rigBuilder.Build();
            rigFpv.gameObject.SetActive(false);
            rigLayer = null;
        }

        sw.Stop();
        string info = $"destroy animation rig took {sw.ElapsedMilliseconds} ms";
        Log.Out(info);
    }
}