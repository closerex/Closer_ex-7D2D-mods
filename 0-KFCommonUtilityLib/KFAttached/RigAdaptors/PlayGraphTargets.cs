#if NotEditor
using UniLinq;
#else
using System.Linq;
#endif
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using System;
using KFCommonUtilityLib;

[AddComponentMenu("KFAttachments/RigAdaptors/PlayGraph Targets")]
public class PlayGraphTargets : AnimationTargetsAbs
{
    [Header("FPV Fields")]
    [SerializeField]
    public Transform itemFpv;
    [SerializeField]
    public Transform attachmentReference;
    [SerializeField]
    private RuntimeAnimatorController weaponRuntimeControllerFpv;
    [SerializeField]
    private ParentName parentNameFpv;

    private Rig[] rigFpv;
    private RigLayer[] rigLayerFpv;
    private Animator itemAnimatorFpv;
    public override Transform ItemFpv { get => itemFpv; protected set => itemFpv = value; }

    public override Transform AttachmentRef { get => attachmentReference; protected set => attachmentReference = value; }

    protected override Animator ItemAnimatorFpv => itemAnimatorFpv;

    public override bool UseGraph => ItemCurrent;

    public override Transform PlayerOriginTransform { get; protected set; }

    public override bool IsRiggedWeapon => false;

    protected override void Awake()
    {
        base.Awake();
        if (!itemFpv)
        {
            return;
        }

        rigFpv = itemFpv.GetComponentsInChildren<Rig>();
#if NotEditor
        if (rigFpv.Length > 0)
        {
            int uid = TypeBasedUID<AnimationTargetsAbs>.UID;
            foreach (var rig in rigFpv)
            {
                rig.gameObject.name += $"_UID_{uid}";
                AnimationRiggingManager.AddRigExcludeName(rig.gameObject.name);
            }
        }
#endif
        rigLayerFpv = new RigLayer[rigFpv.Length];
        itemFpv.gameObject.SetActive(false);
    }

    protected override void Init()
    {
        base.Init();
        if (!itemFpv)
        {
            return;
        }

        itemFpv.SetParent(PlayerAnimatorTrans.parent);
        itemFpv.position = Vector3.zero;
        itemFpv.localPosition = Vector3.zero;
        itemFpv.localRotation = Quaternion.identity;

        PlayerOriginTransform = PlayerAnimatorTrans.FindInAllChildren("Hips");

        if (IsFpv)
        {
            itemAnimatorFpv = PlayerAnimatorTrans.GetComponent<Animator>();
            if (rigFpv.Length > 0)
            {
                foreach (var rig in rigFpv)
                {
                    if (rig.TryGetComponent<RigConverter>(out var rc))
                    {
                        rc.targetRoot = PlayerAnimatorTrans;
                        rc.Rebind();
                    }
                }
            }
        }
        else
        {
            itemAnimatorFpv = null;
        }
    }

    protected override bool SetupFpv()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        itemFpv.SetParent(itemAnimatorFpv.transform.FindInAllChildren(GetParentName(parentNameFpv)));
        itemFpv.position = Vector3.zero;
        itemFpv.localPosition = Vector3.zero;
        itemFpv.localRotation = Quaternion.identity;

        GraphBuilder.InitWeapon(itemFpv, weaponRuntimeControllerFpv, null);
        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
#if NotEditor
        foreach (var layer in rigBuilder.layers)
        {
            if (layer.name == SDCSUtils.IKRIG)
            {
                layer.active = false;
            }
        }
#endif
        if (rigFpv.Length > 0)
        {
            rigBuilder.layers.RemoveAll(r => rigLayerFpv.Any(layer => layer?.name == r.name));
            for (int i = 0; i < rigFpv.Length; i++)
            {
                rigBuilder.layers.Insert(i, rigLayerFpv[i] = new RigLayer(rigFpv[i], true));
            }
        }
        BuildRig(PlayerAnimatorTrans.GetComponent<Animator>(), rigBuilder);

        sw.Stop();
        string info = $"setup fpv animation graph took {sw.ElapsedMilliseconds} ms";
        //info += $"\n{StackTraceUtility.ExtractStackTrace()}";
        Log.Out(info);
        return true;
    }

    protected override void RemoveFpv()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        itemFpv.SetParent(PlayerAnimatorTrans.parent);
        itemFpv.position = Vector3.zero;
        itemFpv.localPosition = Vector3.zero;
        itemFpv.localRotation = Quaternion.identity;

        var rigBuilder = PlayerAnimatorTrans.AddMissingComponent<RigBuilder>();
#if NotEditor
        foreach (var layer in rigBuilder.layers)
        {
            if (layer.name == SDCSUtils.IKRIG)
            {
                layer.active = true;
            }
        }
#endif
        if (rigFpv.Length > 0)
        {
            rigBuilder.layers.RemoveAll(r => rigLayerFpv.Any(layer => layer?.name == r.name));
            Array.Clear(rigLayerFpv, 0, rigLayerFpv.Length);

            //rigFpv.transform.SetParent(transform, false);
            //rigFpv.gameObject.SetActive(false);
        }
        BuildRig(PlayerAnimatorTrans.GetComponent<Animator>(), rigBuilder);

        sw.Stop();
        string info = $"destroy fpv animation graph took {sw.ElapsedMilliseconds} ms";
        //info += $"\n{StackTraceUtility.ExtractStackTrace()}";
        Log.Out(info);
    }
}